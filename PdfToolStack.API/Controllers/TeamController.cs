using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PdfToolStack.Application.Interfaces;
using PdfToolStack.Infrastructure.Services;
using System.Security.Claims;

namespace PdfToolStack.API.Controllers
{
    [ApiController]
    [Route("api/team")]
    [Authorize]
    public class TeamController : ControllerBase
    {
        private readonly ITeamService _teamService;
        private readonly SubscriptionService? _subscriptionService;
        private readonly ILogger<TeamController> _logger;

        public TeamController(
            ITeamService teamService,
            ILogger<TeamController> logger,
            SubscriptionService? subscriptionService = null)
        {
            _teamService = teamService;
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        private string? GetUserId() =>
            User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        private string GetUserEmail() =>
            User.FindFirst("email")?.Value ?? string.Empty;

        private string GetUserName() =>
            User.Identity?.Name ?? GetUserEmail().Split('@')[0];

        // ── Get team ──────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> GetTeam()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var team = await _teamService.GetTeamByOwnerAsync(userId)
                       ?? await _teamService.GetTeamByMemberAsync(userId);

            if (team == null)
                return NotFound(new { error = "No team found." });

            return Ok(new
            {
                id = team.Id,
                name = team.Name,
                maxSeats = team.MaxSeats,
                ownerUserId = team.OwnerUserId,
                isOwner = team.OwnerUserId == userId,
                memberCount = team.Members.Count,
                pendingInvites = team.Invites.Count(i => !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow)
            });
        }

        // ── Initialize team ───────────────────────────────────────────────────────

        [HttpPost("initialize")]
        public async Task<IActionResult> Initialize()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            // Gate: must have Teams subscription
            if (_subscriptionService != null)
            {
                var status = await _subscriptionService.GetStatusAsync(userId);
                if (!status.HasTeams)
                    return Forbid();
            }

            var email = GetUserEmail();
            var name = GetUserName();

            var team = await _teamService.GetOrCreateTeamAsync(userId, email, name);

            return Ok(new
            {
                id = team.Id,
                name = team.Name,
                maxSeats = team.MaxSeats,
                ownerUserId = team.OwnerUserId,
                isOwner = true,
                memberCount = team.Members.Count
            });
        }

        // ── Invite member ─────────────────────────────────────────────────────────

        [HttpPost("invite")]
        public async Task<IActionResult> Invite([FromBody] InviteRequest request)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { error = "Email is required." });

            var team = await _teamService.GetTeamByOwnerAsync(userId);
            if (team == null)
                return BadRequest(new { error = "You don't have a team. Initialize your team first." });

            if (team.OwnerUserId != userId)
                return Forbid();

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var (success, error) = await _teamService.InviteMemberAsync(
                team.Id, request.Email, baseUrl);

            if (!success)
                return BadRequest(new { error });

            return Ok(new { message = $"Invite sent to {request.Email}." });
        }

        // ── Accept invite ─────────────────────────────────────────────────────────

        [HttpPost("accept")]
        public async Task<IActionResult> Accept([FromBody] AcceptInviteRequest request)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Token))
                return BadRequest(new { error = "Token is required." });

            var email = GetUserEmail();
            var name = GetUserName();

            var (success, error) = await _teamService.AcceptInviteAsync(
                request.Token, userId, email, name);

            if (!success)
                return BadRequest(new { error });

            return Ok(new { message = "You have joined the team." });
        }

        // ── Get members + usage ───────────────────────────────────────────────────

        [HttpGet("members")]
        public async Task<IActionResult> GetMembers()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var team = await _teamService.GetTeamByOwnerAsync(userId);
            if (team == null)
                return NotFound(new { error = "Team not found." });

            if (team.OwnerUserId != userId)
                return Forbid();

            var usage = await _teamService.GetMemberUsageAsync(team.Id);
            return Ok(usage);
        }

        // ── Remove member ─────────────────────────────────────────────────────────

        [HttpDelete("member/{memberUserId}")]
        public async Task<IActionResult> RemoveMember(string memberUserId)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var team = await _teamService.GetTeamByOwnerAsync(userId);
            if (team == null)
                return NotFound(new { error = "Team not found." });

            var (success, error) = await _teamService.RemoveMemberAsync(
                team.Id, memberUserId, userId);

            if (!success)
                return BadRequest(new { error });

            return Ok(new { message = "Member removed." });
        }

        // ── Rename team ───────────────────────────────────────────────────────────

        [HttpPut("rename")]
        public async Task<IActionResult> Rename([FromBody] RenameTeamRequest request)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { error = "Team name is required." });

            var team = await _teamService.GetTeamByOwnerAsync(userId);
            if (team == null)
                return NotFound(new { error = "Team not found." });

            var success = await _teamService.RenameTeamAsync(team.Id, userId, request.Name);
            if (!success)
                return BadRequest(new { error = "Could not rename team." });

            return Ok(new { message = "Team renamed." });
        }

        // ── Pending invites ───────────────────────────────────────────────────────

        [HttpGet("invites")]
        public async Task<IActionResult> GetInvites()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var team = await _teamService.GetTeamByOwnerAsync(userId);
            if (team == null)
                return NotFound(new { error = "Team not found." });

            var pending = team.Invites
                .Where(i => !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow)
                .Select(i => new
                {
                    id = i.Id,
                    email = i.Email,
                    expiresAt = i.ExpiresAt,
                    createdAt = i.CreatedAt
                })
                .ToList();

            return Ok(pending);
        }
    }

    public record InviteRequest(string Email);
    public record AcceptInviteRequest(string Token);
    public record RenameTeamRequest(string Name);
}