namespace PdfToolStack.Domain.Entities
{
    /// <summary>
    /// Records a one-time AI credit top-up purchase.
    /// Credits are consumed after the monthly plan allowance is exhausted.
    /// </summary>
    public class AiCreditPurchase
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string StripeSessionId { get; set; } = string.Empty;
        public int CreditsAdded { get; set; }
        public int CreditsUsed { get; set; } = 0;
        public int CreditsRemaining => CreditsAdded - CreditsUsed;
        public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
        /// <summary>Credits expire 90 days after purchase regardless of billing cycle.</summary>
        public DateTime ExpiresAt { get; set; }
    }
}
