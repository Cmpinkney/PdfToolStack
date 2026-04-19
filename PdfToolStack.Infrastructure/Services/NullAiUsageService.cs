using PdfToolStack.Application.Interfaces;

namespace PdfToolStack.Infrastructure.Services
{
    public class NullAiUsageService : IAiUsageService
    {
        public Task<(bool Allowed, int Used, int Limit)> CheckAndLogAsync(
            string userId, string feature, string model, string planType)
            => Task.FromResult((true, 0, 200));

        public Task<(int Used, int Limit)> GetUsageAsync(
            string userId, string planType)
            => Task.FromResult((0, 200));

        public Task<int> GetPurchasedCreditsRemainingAsync(string userId)
            => Task.FromResult(0);

        public Task<bool> RecordCreditPurchaseAsync(
            string userId, string stripeSessionId, int creditsAdded)
            => Task.FromResult(false);
    }
}