using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Application.DTOs
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

        // Additional files for merge/sign operations
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

        // Organize — page reorder/delete/rotate operations
        public List<PageOperationDto> PageOperations { get; set; }
            = new List<PageOperationDto>();

        // Sign — signature image bytes + placement
        public byte[] SignatureBytes { get; set; }
            = Array.Empty<byte>();
        public float SignatureX { get; set; }
        public float SignatureY { get; set; }
        public float SignatureWidth { get; set; }
        public float SignatureHeight { get; set; }
        public int SignaturePageNumber { get; set; } = 1;

        // Edit — text/shape annotations
        public List<PdfAnnotationDto> Annotations { get; set; }
            = new List<PdfAnnotationDto>();

        // Annotate — highlights, drawings, freehand
        public List<PdfHighlightDto> Highlights { get; set; }
            = new List<PdfHighlightDto>();

        // ── Crop ─────────────────────────────────────────────────────────────────
        // Margins in PDF points (1 pt = 1/72 inch).
        // The processor converts these from pixels using a 72dpi default if needed,
        // but the UI sends points directly (user enters mm, UI converts at 2.8346 pt/mm).

        /// <summary>Points to remove from the top of each page.</summary>
        public float CropMarginTop { get; set; } = 0f;

        /// <summary>Points to remove from the right of each page.</summary>
        public float CropMarginRight { get; set; } = 0f;

        /// <summary>Points to remove from the bottom of each page.</summary>
        public float CropMarginBottom { get; set; } = 0f;

        /// <summary>Points to remove from the left of each page.</summary>
        public float CropMarginLeft { get; set; } = 0f;

        /// <summary>
        /// Which pages to crop. null = all pages.
        /// 1-indexed to match PDF convention.
        /// </summary>
        public List<int>? CropPageNumbers { get; set; }
    }

    public class RedactionRegionDto
    {
        public float X1 { get; set; }
        public float Y1 { get; set; }
        public float X2 { get; set; }
        public float Y2 { get; set; }
        public int PageNumber { get; set; } = 1;
    }

    // DTO mirror of Infrastructure.Processors.PageOperation
    // Keeps Application layer free of Infrastructure dependency
    public class PageOperationDto
    {
        public int Order { get; set; }
        public string Type { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public int TargetIndex { get; set; }
        public int RotationDegrees { get; set; }
    }
}
