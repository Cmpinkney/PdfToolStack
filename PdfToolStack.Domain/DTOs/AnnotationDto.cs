using System.Text.Json.Serialization;

namespace PdfToolStack.Domain.DTOs;

public class ApplyAnnotationsRequest
{
    [JsonPropertyName("pdfBase64")]
    public string PdfBase64 { get; set; } = "";
    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }
    [JsonPropertyName("annotations")]
    public List<AnnotationDto> Annotations { get; set; } = new();
}

public class AnnotationDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
    [JsonPropertyName("page")]
    public int Page { get; set; }
    [JsonPropertyName("x")]
    public double X { get; set; }
    [JsonPropertyName("y")]
    public double Y { get; set; }
    [JsonPropertyName("width")]
    public double Width { get; set; }
    [JsonPropertyName("height")]
    public double Height { get; set; }
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    [JsonPropertyName("color")]
    public string Color { get; set; } = "#FF0000";
    [JsonPropertyName("fontSize")]
    public int FontSize { get; set; } = 14;
    [JsonPropertyName("points")]
    public List<InkPoint>? Points { get; set; }
    [JsonPropertyName("scale")]
    public double Scale { get; set; } = 1.5;
}

public class InkPoint
{
    [JsonPropertyName("x")]
    public double X { get; set; }
    [JsonPropertyName("y")]
    public double Y { get; set; }
}