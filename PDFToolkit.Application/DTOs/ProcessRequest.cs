using PdfToolkit.Domain.Enums;

namespace PdfToolkit.Application.DTOs
{
    public class ProcessRequest
    {
        public Guid JobId { get; set; } = Guid.NewGuid();
        public ToolType ToolType { get; set; }
        public byte[] FileBytes { get; set; }
            = Array.Empty<byte>();
        public string FileName { get; set; }
            = string.Empty;
        public long FileSizeBytes { get; set; }

        // Additional files for merge operations
        public List<byte[]> AdditionalFiles { get; set; }
            = new List<byte[]>();
        public List<string> AdditionalFileNames { get; set; }
            = new List<string>();

        // Form field data for fill operations
        public Dictionary<string, string> FormFields { get; set; }
            = new Dictionary<string, string>();

        // Redaction regions
        public List<RedactionRegionDto> RedactionRegions { get; set; }
            = new List<RedactionRegionDto>();
    }

    // DTO version — no dependency on Domain.Entities
    public class RedactionRegionDto
    {
        public float X1 { get; set; }
        public float Y1 { get; set; }
        public float X2 { get; set; }
        public float Y2 { get; set; }
        public int PageNumber { get; set; } = 1;
    }
}
