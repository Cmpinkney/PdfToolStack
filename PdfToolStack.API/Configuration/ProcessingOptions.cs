namespace PdfToolStack.API.Configuration
{
    public class ProcessingOptions
    {
        public const string SectionName = "Processing";
        public long MaxFileSizeBytes { get; set; }
            = 50 * 1024 * 1024; // 50MB default
        public int MaxRequestsPerHour { get; set; } = 10;
        public int ProcessingTimeoutSeconds { get; set; } = 120;
        public int AiMaxRequestsPerHour { get; set; } = 20;
    }
}
