using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PdfToolStack.Application.Interfaces;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Infrastructure.Data;
using System.Security.Cryptography;
using PdfToolStack.Application.DTOs;

namespace PdfToolStack.Infrastructure.Services
{
    public class TeamService : ITeamService
    {
        private readonly AppDbContext _db;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;

        public TeamService(AppDbContext db, IEmailService emailService, IConfiguration config)
        {
            _db = db;
            _emailService = emailService;
            _config = config;
        }

        // ── Get or create team for owner ─────────────────────────────────────────

        public async Task<Team?> GetTeamByOwnerAsync(string ownerUserId)
        {
            return await _db.Teams
                .Include(t => t.Members)
                .Include(t => t.Invites)
                .FirstOrDefaultAsync(t => t.OwnerUserId == ownerUserId);
        }

        public async Task<Team?> GetTeamByMemberAsync(string userId)
        {
            var member = await _db.TeamMembers
                .Include(m => m.Team)
                    .ThenInclude(t => t.Members)
                .Include(m => m.Team)
                    .ThenInclude(t => t.Invites)
                .FirstOrDefaultAsync(m => m.UserId == userId);

            return member?.Team;
        }

        public async Task<Team> GetOrCreateTeamAsync(
            string ownerUserId, string ownerEmail, string ownerName)
        {
            var existing = await _db.Teams
                .Include(t => t.Members)
                .Include(t => t.Invites)
                .FirstOrDefaultAsync(t => t.OwnerUserId == ownerUserId);

            if (existing != null)
                return existing;

            var team = new Team
            {
                OwnerUserId = ownerUserId,
                Name = $"{ownerName}'s Team",
                MaxSeats = 5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Teams.Add(team);
            await _db.SaveChangesAsync();

            // Add owner as admin member
            _db.TeamMembers.Add(new TeamMember
            {
                TeamId = team.Id,
                UserId = ownerUserId,
                Email = ownerEmail,
                Name = ownerName,
                Role = "admin",
                JoinedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            // Reload with members
            return (await _db.Teams
                .Include(t => t.Members)
                .Include(t => t.Invites)
                .FirstOrDefaultAsync(t => t.Id == team.Id))!;
        }

        // ── Invite ────────────────────────────────────────────────────────────────

        public async Task<(bool Success, string Error)> InviteMemberAsync(
            int teamId, string inviteEmail, string baseUrl)
        {
            var team = await _db.Teams
                .Include(t => t.Members)
                .Include(t => t.Invites)
                .FirstOrDefaultAsync(t => t.Id == teamId);

            if (team == null)
                return (false, "Team not found.");

            var activeMembers = team.Members.Count;
            if (activeMembers >= team.MaxSeats)
                return (false, $"Seat limit reached ({team.MaxSeats} seats). Upgrade to add more.");

            var alreadyMember = team.Members
                .Any(m => m.Email.Equals(inviteEmail, StringComparison.OrdinalIgnoreCase));
            if (alreadyMember)
                return (false, "This person is already a team member.");

            // Cancel any existing pending invite for this email
            var existingInvite = team.Invites
                .FirstOrDefault(i =>
                    i.Email.Equals(inviteEmail, StringComparison.OrdinalIgnoreCase) &&
                    !i.IsAccepted &&
                    i.ExpiresAt > DateTime.UtcNow);

            if (existingInvite != null)
                _db.TeamInvites.Remove(existingInvite);

            var token = GenerateToken();
            var invite = new TeamInvite
            {
                TeamId = teamId,
                Email = inviteEmail,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            _db.TeamInvites.Add(invite);
            await _db.SaveChangesAsync();

            var inviteUrl = $"{baseUrl}/team/accept?token={token}";
            await _emailService.SendTeamInviteEmailAsync(
                inviteEmail, team.Name, inviteUrl);

            return (true, string.Empty);
        }

        // ── Accept invite ─────────────────────────────────────────────────────────

        public async Task<(bool Success, string Error)> AcceptInviteAsync(
            string token, string userId, string userEmail, string userName)
        {
            var invite = await _db.TeamInvites
                .Include(i => i.Team)
                    .ThenInclude(t => t.Members)
                .FirstOrDefaultAsync(i => i.Token == token);

            if (invite == null)
                return (false, "Invite not found or already used.");

            if (invite.IsAccepted)
                return (false, "This invite has already been accepted.");

            if (invite.ExpiresAt < DateTime.UtcNow)
                return (false, "This invite has expired. Ask your team admin to send a new one.");

            if (!invite.Email.Equals(userEmail, StringComparison.OrdinalIgnoreCase))
                return (false, "This invite was sent to a different email address.");

            if (invite.Team.Members.Count >= invite.Team.MaxSeats)
                return (false, "Team is full. Ask your admin to upgrade the plan.");

            var alreadyMember = await _db.TeamMembers
                .AnyAsync(m => m.TeamId == invite.TeamId && m.UserId == userId);

            if (alreadyMember)
            {
                invite.AcceptedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return (true, string.Empty);
            }

            _db.TeamMembers.Add(new TeamMember
            {
                TeamId = invite.TeamId,
                UserId = userId,
                Email = userEmail,
                Name = userName,
                Role = "member",
                JoinedAt = DateTime.UtcNow
            });

            invite.AcceptedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return (true, string.Empty);
        }

        // ── Remove member ─────────────────────────────────────────────────────────

        public async Task<(bool Success, string Error)> RemoveMemberAsync(
            int teamId, string memberUserId, string requestingUserId)
        {
            var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId);
            if (team == null)
                return (false, "Team not found.");

            if (team.OwnerUserId != requestingUserId)
                return (false, "Only the team admin can remove members.");

            if (memberUserId == requestingUserId)
                return (false, "You cannot remove yourself from the team.");

            var member = await _db.TeamMembers
                .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == memberUserId);

            if (member == null)
                return (false, "Member not found.");

            _db.TeamMembers.Remove(member);
            await _db.SaveChangesAsync();

            return (true, string.Empty);
        }

        // ── Usage ─────────────────────────────────────────────────────────────────

        public async Task<List<TeamMemberUsageDto>> GetMemberUsageAsync(int teamId)
        {
            var members = await _db.TeamMembers
                .Where(m => m.TeamId == teamId)
                .ToListAsync();

            var result = new List<TeamMemberUsageDto>();
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            foreach (var member in members)
            {
                var aiUsed = await _db.AiUsageLogs
                    .CountAsync(a =>
                        a.UserId == member.UserId &&
                        a.UsedAt >= monthStart);

                var docsProcessed = await _db.DownloadHistory
                    .CountAsync(d =>
                        d.UserId == member.UserId &&
                        d.ProcessedAt >= monthStart);

                result.Add(new TeamMemberUsageDto
                {
                    UserId = member.UserId,
                    Email = member.Email,
                    Name = member.Name,
                    Role = member.Role,
                    JoinedAt = member.JoinedAt,
                    AiUsedThisMonth = aiUsed,
                    DocsThisMonth = docsProcessed
                });
            }

            return result;
        }

        // ── Rename team ───────────────────────────────────────────────────────────

        public async Task<bool> RenameTeamAsync(int teamId, string ownerUserId, string newName)
        {
            var team = await _db.Teams
                .FirstOrDefaultAsync(t => t.Id == teamId && t.OwnerUserId == ownerUserId);

            if (team == null) return false;

            team.Name = newName;
            team.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        // ── Check membership ──────────────────────────────────────────────────────

        public async Task<bool> IsTeamMemberAsync(string userId)
        {
            return await _db.TeamMembers.AnyAsync(m => m.UserId == userId);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }
    }
}