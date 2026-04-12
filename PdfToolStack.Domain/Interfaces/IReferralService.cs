using PdfToolStack.Domain.Entities;

namespace PdfToolStack.Domain.Interfaces
{
    public interface IReferralService
    {
        Task<string> GetOrCreateReferralCodeAsync(
            string userId,
            CancellationToken ct = default);

        Task<Referral?> GetReferralByCodeAsync(
            string code,
            CancellationToken ct = default);

        Task TrackClickAsync(
            string code, string? referredUserId,
            CancellationToken ct = default);

        Task ConvertReferralAsync(
            string referredUserId, string referredEmail,
            CancellationToken ct = default);

        Task<List<Referral>> GetReferrerStatsAsync(
            string referrerId,
            CancellationToken ct = default);
    }
}