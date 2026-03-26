namespace PdfToolkit.Application.DTOs
{
    public class PdfAnnotationDto
    {
        public string Type { get; set; } = "text";
        public int PageNumber { get; set; } = 1;
        public float X { get; set; }
        public float Y { get; set; }
        public float X2 { get; set; }
        public float Y2 { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string? Text { get; set; }
        public float FontSize { get; set; } = 12f;
        public float LineWidth { get; set; } = 1f;
        public string? Color { get; set; }
    }

    public class PdfHighlightDto
    {
        public string Type { get; set; } = "highlight";
        public int PageNumber { get; set; } = 1;
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float LineWidth { get; set; } = 2f;
        public string? Color { get; set; }
        public List<PointDto>? Points { get; set; }
    }

    public class PointDto
    {
        public float X { get; set; }
        public float Y { get; set; }
    }
}