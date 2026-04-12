namespace PdfToolStack.API.Configuration
{
    public class ProcessingOptions
    {
        public const string SectionName = "Processing";
        public long MaxFileSizeBytes { get; set; }
            = 50 * 1024 * 1024;

        // Anonymous (IP-based) limits
        public int MaxRequestsPerHour { get; set; } = 20;
        public int AiMaxRequestsPerHour { get; set; } = 5;

        // Authenticated (user-based) limits
        public int AuthenticatedMaxRequestsPerHour { get; set; } = 200;
        public int AuthenticatedAiMaxRequestsPerHour { get; set; } = 50;

        public int ProcessingTimeoutSeconds { get; set; } = 120;
    }
}