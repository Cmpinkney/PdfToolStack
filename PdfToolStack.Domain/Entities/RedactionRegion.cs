namespace PdfToolStack.Domain.Entities
{
    public class RedactionRegion
    {
        public float X1 { get; set; }
        public float Y1 { get; set; }
        public float X2 { get; set; }
        public float Y2 { get; set; }
        public int PageNumber { get; set; } = 1;
    }
}
