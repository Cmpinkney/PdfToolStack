namespace PdfToolkit.API.Configuration
{
    public class StripeOptions
    {
        public const string SectionName = "Stripe";
        public string PublishableKey { get; set; }
            = string.Empty;
        public string SecretKey { get; set; }
            = string.Empty;
        public string LargeFilePriceId { get; set; }
            = string.Empty;
        public string Currency { get; set; } = "usd";
    }

    public class FileLimit
    {
        public const string SectionName = "FileLimit";
        public long FreeTierMaxBytes { get; set; }
            = 26214400; // 25MB
        public long PaidTierMaxBytes { get; set; }
            = 524288000; // 500MB
    }
}
