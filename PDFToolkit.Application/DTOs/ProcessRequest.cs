using PDFToolkit.Domain.Enums;

namespace PDFToolkit.Application.DTOs
{
    public class ProcessRequest
    {
        public Guid JobId { get; set; } = Guid.NewGuid();
        public ToolType ToolType { get; set; }
        public byte[] FileBytes { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
    }
}
