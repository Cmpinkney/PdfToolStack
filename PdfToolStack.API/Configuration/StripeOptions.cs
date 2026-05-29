namespace PdfToolStack.API.Configuration
{
    public class StripeOptions
    {
        public const string SectionName = "Stripe";

        public string PublishableKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;

        // Legacy price IDs — kept for existing subscribers
        public string ProMonthlyPriceId { get; set; } = string.Empty;
        public string ProYearlyPriceId { get; set; } = string.Empty;

        // Current price IDs — used for all new checkouts
        public string ProMonthlyPriceIdV2 { get; set; } = string.Empty;
        public string ProYearlyPriceIdV2 { get; set; } = string.Empty;

        // Teams plan
        public string TeamsMonthlyPriceId { get; set; } = string.Empty;
        public string BundleMonthlyPriceId { get; set; } = string.Empty;
        public string TeamsYearlyPriceId { get; set; } = string.Empty;

        // AI Credit Top-Up — one-time purchases
        // Create these as one-time prices in Stripe (not recurring)
        public string AiCredits50PriceId { get; set; } = string.Empty;   // $9.99 = 50 credits
        public string AiCredits200PriceId { get; set; } = string.Empty;  // $29.99 = 200 credits
        public string AiDayPassPriceId { get; set; } = string.Empty;  // $4.99 = AI day pass
        public string BatchUnlockPriceId { get; set; } = string.Empty;  // $4.99 one time batch process
        public string LargeFilePriceId { get; set; } = string.Empty; // large file $1.99


        public string Currency { get; set; } = "usd";
    }

    public sealed class FileLimit
    {
        public const string SectionName = "FileLimits";

        public long FreeTierMaxBytes { get; set; } = 26214400;   // 25 MB

        public long PaidTierMaxBytes { get; set; } = 524288000;  // 500 MB
    }
}
