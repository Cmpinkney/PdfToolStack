using Microsoft.EntityFrameworkCore;
using PdfToolStack.Application.Interfaces;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Infrastructure.Data;

namespace PdfToolStack.Infrastructure.Services
{
    public class AiUsageService : IAiUsageService
    {
        private readonly AppDbContext _db;
        private const int ProMonthlyLimit = 50;
        private const int TeamsMonthlyLimit = 200;

        public AiUsageService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<(bool Allowed, int Used, int Limit)>
            CheckAndLogAsync(string userId, string feature,
                string model, string planType)
        {
            var limit = planType == "teams"
                ? TeamsMonthlyLimit : ProMonthlyLimit;

            // Count usage this calendar month
            var monthStart = new DateTime(
                DateTime.UtcNow.Year,
                DateTime.UtcNow.Month, 1);

            var used = await _db.AiUsageLogs
                .CountAsync(l => l.UserId == userId
                    && l.UsedAt >= monthStart);

            if (used >= limit)
                return (false, used, limit);

            // Log the usage
            _db.AiUsageLogs.Add(new AiUsageLog
            {
                UserId = userId,
                Feature = feature,
                Model = model,
                UsedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return (true, used + 1, limit);
        }

        public async Task<(int Used, int Limit)>
            GetUsageAsync(string userId, string planType)
        {
            var limit = planType == "teams"
                ? TeamsMonthlyLimit : ProMonthlyLimit;

            var monthStart = new DateTime(
                DateTime.UtcNow.Year,
                DateTime.UtcNow.Month, 1);

            var used = await _db.AiUsageLogs
                .CountAsync(l => l.UserId == userId
                    && l.UsedAt >= monthStart);

            return (used, limit);
        }
    }
}