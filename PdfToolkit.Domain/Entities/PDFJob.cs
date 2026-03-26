using PdfToolkit.Domain.Enums;

namespace PdfToolkit.Domain.Entities
{
    public class PdfJob
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ToolType ToolType { get; set; }
        public JobStatus Status { get; set; } = JobStatus.Pending;
        public string OriginalFileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string? OutputBlobUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
