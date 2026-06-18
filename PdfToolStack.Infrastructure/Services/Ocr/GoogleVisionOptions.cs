namespace PdfToolStack.Infrastructure.Services.Ocr
{
    public sealed class GoogleVisionOptions
    {
        public const string SectionName = "GoogleVision";

        public bool Enabled { get; set; }
        public string? ProjectId { get; set; }
        public string? CredentialsPath { get; set; }
        public int MinTesseractCharsBeforeFallback { get; set; } = 800;
        public int MaxFallbackPagesForFreeUsers { get; set; } = 3;
        public int MaxFallbackPagesForProUsers { get; set; } = 25;
    }
}
