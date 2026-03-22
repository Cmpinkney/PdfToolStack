using PDFToolkit.Domain.Enums;

namespace PDFToolkit.Application.DTOs
{
    public class JobStatusResponse
    {
        public Guid JobId { get; set; }
        public JobStatus Status { get; set; }
        public string? DownloadUrl { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
