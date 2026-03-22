using PdfToolkit.Domain.Entities;

namespace PdfToolkit.Application.DTOs
{
    public class DetectFieldsResponse
    {
        public bool HasFields { get; set; }
        public List<PdfFormField> Fields { get; set; }
            = new List<PdfFormField>();
        public string? ErrorMessage { get; set; }
    }
}
