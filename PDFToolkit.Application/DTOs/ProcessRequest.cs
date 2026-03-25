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
        // Page numbers for extract/delete operations
        public List<int>? PageNumbers { get; set; }

        // Rotation
        public int Rotation { get; set; } = 90;

        // Watermark
        public string WatermarkText { get; set; } = "CONFIDENTIAL";
        public float WatermarkOpacity { get; set; } = 0.3f;
        public float WatermarkFontSize { get; set; } = 48f;

        // Number pages
        public string PageNumberPosition { get; set; } = "bottom-center";
        public int PageNumberStart { get; set; } = 1;

        // Password operations
        public string? Password { get; set; }
        public string UserPassword { get; set; } = string.Empty;
        public string OwnerPassword { get; set; } = string.Empty;
        public bool AllowPrinting { get; set; } = true;
        public bool AllowCopying { get; set; } = false;

        // Split range
        public int? SplitFromPage { get; set; }
        public int? SplitToPage { get; set; }
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
