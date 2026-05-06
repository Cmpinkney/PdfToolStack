namespace PdfToolStack.Infrastructure.Configuration
{
    public sealed class GoogleVisionOptions
    {
        public const string SectionName = "GoogleVision";

        public bool Enabled { get; set; }
        public string ProjectId { get; set; } = string.Empty;
        public string CredentialsPath { get; set; } = string.Empty;
        public int MinTesseractCharsBeforeFallback { get; set; } = 800;
        public int MaxFallbackPagesForFreeUsers { get; set; } = 3;
        public int MaxFallbackPagesForProUsers { get; set; } = 25;
    }
}
