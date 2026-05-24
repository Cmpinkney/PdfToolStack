using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PdfToolStack.Application.Interfaces;
using PdfToolStack.Infrastructure.Data;

namespace PdfToolStack.Infrastructure.Services
{
    public class FeatureAccessService : IFeatureAccessService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public FeatureAccessService(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public async Task<bool> HasProAccessAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            if (IsAdmin(userId))
                return true;

            var sub = await _db.UserSubscriptions
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (sub == null)
                return false;

            return sub.CurrentPeriodEnd > DateTime.UtcNow &&
                   (string.Equals(sub.Status, "active", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sub.Status, "trialing", StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> HasLargeFileUnlockAsync(string userId)
        {
            if (await HasProAccessAsync(userId))
                return true;

            return await _db.OneTimePurchases.AnyAsync(x =>
                x.UserId == userId &&
                x.PurchaseType == "LargeFileUnlock" &&
                !x.IsConsumed &&
                x.UsesRemaining > 0 &&
                (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow));
        }

        public async Task<bool> HasAiDayPassAsync(string userId)
        {
            if (await HasProAccessAsync(userId))
                return true;

            return await _db.OneTimePurchases.AnyAsync(x =>
                x.UserId == userId &&
                x.PurchaseType == "AiDayPass" &&
                !x.IsConsumed &&
                x.UsesRemaining > 0 &&
                x.ExpiresAt != null &&
                x.ExpiresAt > DateTime.UtcNow);
        }

        public async Task<int> GetAiDayPassUsesRemainingAsync(string userId)
        {
            var pass = await _db.OneTimePurchases
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId &&
                    x.PurchaseType == "AiDayPass" &&
                    !x.IsConsumed &&
                    x.UsesRemaining > 0 &&
                    x.ExpiresAt != null &&
                    x.ExpiresAt > DateTime.UtcNow)
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            return pass?.UsesRemaining ?? 0;
        }

        public async Task<int> GetAiCreditsRemainingAsync(string userId)
        {
            var purchases = await _db.AiCreditPurchases
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId &&
                    x.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            return purchases.Sum(x => x.CreditsAdded - x.CreditsUsed);
        }

        public async Task<bool> HasBatchUnlockAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            return await _db.OneTimePurchases.AnyAsync(x =>
                x.UserId == userId &&
                x.PurchaseType == "BatchUnlock" &&
                !x.IsConsumed &&
                x.UsesRemaining > 0 &&
                (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow));
        }

        public async Task<bool> ConsumeLargeFileUnlockAsync(string userId)
        {
            var unlock = await _db.OneTimePurchases
                .Where(x =>
                    x.UserId == userId &&
                    x.PurchaseType == "LargeFileUnlock" &&
                    !x.IsConsumed &&
                    x.UsesRemaining > 0 &&
                    (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow))
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (unlock == null)
                return false;

            unlock.UsesRemaining--;

            if (unlock.UsesRemaining <= 0)
                unlock.IsConsumed = true;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task ConsumeAiDayPassUseAsync(string userId)
        {
            var pass = await _db.OneTimePurchases
                .Where(x =>
                    x.UserId == userId &&
                    x.PurchaseType == "AiDayPass" &&
                    !x.IsConsumed &&
                    x.UsesRemaining > 0 &&
                    x.ExpiresAt != null &&
                    x.ExpiresAt > DateTime.UtcNow)
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (pass == null)
                return;

            pass.UsesRemaining--;

            if (pass.UsesRemaining <= 0)
                pass.IsConsumed = true;

            await _db.SaveChangesAsync();
        }

        public async Task ConsumeBatchUnlockAsync(string userId)
        {
            var unlock = await _db.OneTimePurchases
                .Where(x =>
                    x.UserId == userId &&
                    x.PurchaseType == "BatchUnlock" &&
                    !x.IsConsumed &&
                    x.UsesRemaining > 0 &&
                    (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow))
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (unlock == null)
                return;

            unlock.UsesRemaining--;

            if (unlock.UsesRemaining <= 0)
                unlock.IsConsumed = true;

            await _db.SaveChangesAsync();
        }

        private bool IsAdmin(string userId)
        {
            var adminIds = _config["AdminUserIds"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(adminIds))
                return false;

            return adminIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(userId, StringComparer.OrdinalIgnoreCase);
        }
    }
}
