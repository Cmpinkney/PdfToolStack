namespace PdfToolStack.Domain.Entities
{
    public class OneTimePurchase
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } = string.Empty;
        public string PurchaseType { get; set; } = string.Empty; // LargeFileUnlock, AiDayPass, BatchUnlock
        public string StripeSessionId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }

        public int UsesRemaining { get; set; } = 0;
        public bool IsConsumed { get; set; } = false;
    }
}