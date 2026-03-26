namespace PdfToolkit.Domain.Entities
{
    public class DownloadHistory
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ToolType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string? DownloadUrl { get; set; }
    }
}