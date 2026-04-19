namespace PdfToolStack.Application.Interfaces
{
    public interface IAiUsageService
    {
        /// <summary>
        /// Checks if the user is allowed to make an AI request.
        /// Consumes from monthly plan allowance first, then from purchased credits.
        /// </summary>
        Task<(bool Allowed, int Used, int Limit)> CheckAndLogAsync(
            string userId, string feature,
            string model, string planType);

        /// <summary>
        /// Returns current month usage and plan limit (not including top-up credits).
        /// </summary>
        Task<(int Used, int Limit)> GetUsageAsync(
            string userId, string planType);

        /// <summary>
        /// Returns remaining purchased top-up credits for the user (non-expired).
        /// </summary>
        Task<int> GetPurchasedCreditsRemainingAsync(string userId);

        /// <summary>
        /// Records a completed credit top-up purchase from Stripe.
        /// Returns false if the session was already recorded (idempotent).
        /// </summary>
        Task<bool> RecordCreditPurchaseAsync(
            string userId, string stripeSessionId, int creditsAdded);
    }
}
