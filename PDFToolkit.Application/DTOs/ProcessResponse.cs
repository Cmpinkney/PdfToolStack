using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PDFToolkit.Application.DTOs
{
    public class ProcessResponse
    {
        public Guid JobId { get; set; }
        public bool IsSuccess { get; set; }
        public string? DownloadUrl { get; set; }
        public string? ErrorMessage { get; set; }
        public long OriginalSizeBytes { get; set; }
        public long OutputSizeBytes { get; set; }
        public double CompressionRatio =>
            OriginalSizeBytes > 0
                ? Math.Round((1 - (double)OutputSizeBytes / OriginalSizeBytes) * 100, 1)
                : 0;
    }
}
