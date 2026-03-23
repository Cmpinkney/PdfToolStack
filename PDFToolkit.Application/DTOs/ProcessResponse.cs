namespace PdfToolkit.Application.DTOs
{
    public class ProcessResponse
    {
        public Guid JobId { get; set; }
        public bool IsSuccess { get; set; }
        public string? DownloadUrl { get; set; }
        public string? ErrorMessage { get; set; }
        public long OriginalSizeBytes { get; set; }
        public long OutputSizeBytes { get; set; }
        public double CompressionRatio
        {
            get
            {
                if (OriginalSizeBytes == 0) return 0;
                return Math.Round(
                    (1 - (double)OutputSizeBytes
                        / OriginalSizeBytes) * 100, 1);
            }
        }
    }
}
