namespace PdfToolStack.Web.Models
{
    public sealed class UploadedBrowserFile
    {
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
    }
}