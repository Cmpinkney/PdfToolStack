namespace PdfToolStack.API.Configuration
{
    public class StripeOptions
    {
        public const string SectionName = "Stripe";

        public string PublishableKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
        public string LargeFilePriceId { get; set; } = string.Empty;

        // Legacy price IDs — kept for existing subscribers
        public string ProMonthlyPriceId { get; set; } = string.Empty;
        public string ProYearlyPriceId { get; set; } = string.Empty;

        // Current price IDs — used for all new checkouts
        public string ProMonthlyPriceIdV2 { get; set; } = string.Empty;
        public string ProYearlyPriceIdV2 { get; set; } = string.Empty;

        // Teams plan
        public string TeamsMonthlyPriceId { get; set; } = string.Empty;

        public string Currency { get; set; } = "usd";
    }

    public sealed class FileLimit
    {
        public const string SectionName = "FileLimits";

        public long FreeTierMaxBytes { get; set; } = 26214400;   // 25 MB

        public long PaidTierMaxBytes { get; set; } = 524288000;  // 500 MB
    }
}