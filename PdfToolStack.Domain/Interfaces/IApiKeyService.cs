using PdfToolStack.Domain.Entities;

namespace PdfToolStack.Domain.Interfaces
{
    public interface IApiKeyService
    {
        Task<(ApiKey key, string rawKey)> CreateKeyAsync(
            string userId, string name,
            CancellationToken ct = default);

        Task<ApiKey?> ValidateKeyAsync(
            string rawKey,
            CancellationToken ct = default);

        Task<List<ApiKey>> GetUserKeysAsync(
            string userId,
            CancellationToken ct = default);

        Task RevokeKeyAsync(
            int keyId, string userId,
            CancellationToken ct = default);

        Task IncrementUsageAsync(
            int keyId,
            CancellationToken ct = default);
    }
}