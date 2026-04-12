using Microsoft.EntityFrameworkCore;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;

namespace PdfToolStack.Infrastructure.Services
{
    public class ApiKeyService : IApiKeyService
    {
        private readonly AppDbContext _db;

        public ApiKeyService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<(ApiKey key, string rawKey)> CreateKeyAsync(
            string userId, string name,
            CancellationToken ct = default)
        {
            // Generate a secure random key: pts_live_<32 random bytes>
            var rawBytes = RandomNumberGenerator.GetBytes(32);
            var rawKey = $"pts_live_{Convert.ToBase64String(rawBytes)
                .Replace("+", "")
                .Replace("/", "")
                .Replace("=", "")[..32]}";

            var prefix = rawKey[..16];
            var hash = HashKey(rawKey);

            var key = new ApiKey
            {
                UserId = userId,
                KeyHash = hash,
                KeyPrefix = prefix,
                Name = name,
                IsActive = true,
                RequestsThisMonth = 0,
                MonthlyLimit = 1000,
                CurrentMonthStart = new DateTime(
                    DateTime.UtcNow.Year,
                    DateTime.UtcNow.Month, 1),
                CreatedAt = DateTime.UtcNow
            };

            _db.ApiKeys.Add(key);
            await _db.SaveChangesAsync(ct);

            return (key, rawKey);
        }

        public async Task<ApiKey?> ValidateKeyAsync(
            string rawKey,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(rawKey) ||
                !rawKey.StartsWith("pts_live_"))
                return null;

            var hash = HashKey(rawKey);

            var key = await _db.ApiKeys
                .FirstOrDefaultAsync(k =>
                    k.KeyHash == hash &&
                    k.IsActive, ct);

            if (key == null) return null;

            // Reset monthly counter if new month
            var now = DateTime.UtcNow;
            if (now >= key.CurrentMonthStart.AddMonths(1))
            {
                key.RequestsThisMonth = 0;
                key.CurrentMonthStart = new DateTime(
                    now.Year, now.Month, 1);
                await _db.SaveChangesAsync(ct);
            }

            // Check monthly limit
            if (key.RequestsThisMonth >= key.MonthlyLimit)
                return null;

            return key;
        }

        public async Task<List<ApiKey>> GetUserKeysAsync(
            string userId,
            CancellationToken ct = default)
        {
            return await _db.ApiKeys
                .Where(k => k.UserId == userId)
                .OrderByDescending(k => k.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task RevokeKeyAsync(
            int keyId, string userId,
            CancellationToken ct = default)
        {
            var key = await _db.ApiKeys
                .FirstOrDefaultAsync(k =>
                    k.Id == keyId &&
                    k.UserId == userId, ct);

            if (key == null) return;

            key.IsActive = false;
            key.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        public async Task IncrementUsageAsync(
            int keyId,
            CancellationToken ct = default)
        {
            var key = await _db.ApiKeys
                .FirstOrDefaultAsync(k => k.Id == keyId, ct);

            if (key == null) return;

            key.RequestsThisMonth++;
            key.LastUsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        private static string HashKey(string rawKey)
        {
            var bytes = SHA256.HashData(
                Encoding.UTF8.GetBytes(rawKey));
            return Convert.ToBase64String(bytes);
        }
    }
}