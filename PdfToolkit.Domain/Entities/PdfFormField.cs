namespace PdfToolkit.Domain.Entities
{
    public class PdfFormField
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string FieldType { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public List<string> Options { get; set; } = new();
    }
}
