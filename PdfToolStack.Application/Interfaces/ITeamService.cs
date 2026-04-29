using PdfToolStack.Application.DTOs;
using PdfToolStack.Domain.Entities;

namespace PdfToolStack.Application.Interfaces
{
    public interface ITeamService
    {
        Task<Team?> GetTeamByOwnerAsync(string ownerUserId);
        Task<Team?> GetTeamByMemberAsync(string userId);
        Task<Team> GetOrCreateTeamAsync(string ownerUserId, string ownerEmail, string ownerName);
        Task<(bool Success, string Error)> InviteMemberAsync(int teamId, string inviteEmail, string baseUrl);
        Task<(bool Success, string Error)> AcceptInviteAsync(string token, string userId, string userEmail, string userName);
        Task<(bool Success, string Error)> RemoveMemberAsync(int teamId, string memberUserId, string requestingUserId);
        Task<List<TeamMemberUsageDto>> GetMemberUsageAsync(int teamId);
        Task<bool> RenameTeamAsync(int teamId, string ownerUserId, string newName);
        Task<bool> IsTeamMemberAsync(string userId);
    }
}