namespace PdfToolStack.Domain.Entities
{
    public class Referral
    {
        public int Id { get; set; }
        public string ReferrerId { get; set; } = string.Empty;
        public string ReferralCode { get; set; } = string.Empty;
        public string? ReferredUserId { get; set; }
        public string? ReferredEmail { get; set; }
        public ReferralStatus Status { get; set; }
            = ReferralStatus.Pending;
        public DateTime CreatedAt { get; set; }
        public DateTime? ConvertedAt { get; set; }
        public DateTime? RewardedAt { get; set; }
        public string? StripeDiscountId { get; set; }
    }

    public enum ReferralStatus
    {
        Pending = 0,    // Link clicked, not yet subscribed
        Converted = 1,  // Referred user subscribed
        Rewarded = 2    // Referrer received free month
    }
}