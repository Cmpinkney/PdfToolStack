using Microsoft.EntityFrameworkCore;
using PdfToolStack.Application.Interfaces;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Infrastructure.Data;

namespace PdfToolStack.Infrastructure.Services
{
    public class AiUsageService : IAiUsageService
    {
        private readonly AppDbContext _db;
        private const int FreeMonthlyLimit = 5;
        private const int ProMonthlyLimit = 200;
        private const int TeamsMonthlyLimit = 500;

        public AiUsageService(AppDbContext db) => _db = db;

        public async Task<(bool Allowed, int Used, int Limit)>
            CheckAndLogAsync(string userId, string feature, string model, string planType)
        {
            var limit = planType switch
            {
                "teams" => TeamsMonthlyLimit,
                "monthly" or "yearly" => ProMonthlyLimit,
                _ => FreeMonthlyLimit   
            };
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var used = await _db.AiUsageLogs
                .CountAsync(l => l.UserId == userId && l.UsedAt >= monthStart);

            if (used < limit)
            {
                _db.AiUsageLogs.Add(new AiUsageLog { UserId = userId, Feature = feature, Model = model, UsedAt = DateTime.UtcNow });
                await _db.SaveChangesAsync();
                return (true, used + 1, limit);
            }

            // Try purchased top-up credits (FIFO - oldest first)
            var now = DateTime.UtcNow;
            var topUp = await _db.AiCreditPurchases
                .Where(p => p.UserId == userId && p.ExpiresAt > now && p.CreditsUsed < p.CreditsAdded)
                .OrderBy(p => p.PurchasedAt)
                .FirstOrDefaultAsync();

            if (topUp != null)
            {
                topUp.CreditsUsed += 1;
                _db.AiUsageLogs.Add(new AiUsageLog { UserId = userId, Feature = feature, Model = model, UsedAt = DateTime.UtcNow });
                await _db.SaveChangesAsync();
                return (true, used + 1, limit);
            }

            return (false, used, limit);
        }

        public async Task<(int Used, int Limit)> GetUsageAsync(string userId, string planType)
        {
            var limit = planType == "teams" ? TeamsMonthlyLimit : ProMonthlyLimit;
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var used = await _db.AiUsageLogs.CountAsync(l => l.UserId == userId && l.UsedAt >= monthStart);
            return (used, limit);
        }

        public async Task<int> GetPurchasedCreditsRemainingAsync(string userId)
        {
            var now = DateTime.UtcNow;
            return await _db.AiCreditPurchases
                .Where(p => p.UserId == userId && p.ExpiresAt > now && p.CreditsUsed < p.CreditsAdded)
                .SumAsync(p => p.CreditsAdded - p.CreditsUsed);
        }

        public async Task<bool> RecordCreditPurchaseAsync(string userId, string stripeSessionId, int creditsAdded)
        {
            if (await _db.AiCreditPurchases.AnyAsync(p => p.StripeSessionId == stripeSessionId))
                return false;

            _db.AiCreditPurchases.Add(new AiCreditPurchase
            {
                UserId = userId,
                StripeSessionId = stripeSessionId,
                CreditsAdded = creditsAdded,
                CreditsUsed = 0,
                PurchasedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(90)
            });
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
