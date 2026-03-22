using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PDFToolkit.Domain.Entities
{
    public class ProcessingResult
    {
        public bool IsSuccess { get; set; }
        public byte[]? OutputBytes { get; set; }
        public string? ErrorMessage { get; set; }
        public long OriginalSizeBytes { get; set; }
        public long OutputSizeBytes { get; set; }

        public static ProcessingResult Success(byte[] outputBytes, long originalSize) =>
            new ProcessingResult
            {
                IsSuccess = true,
                OutputBytes = outputBytes,
                OriginalSizeBytes = originalSize,
                OutputSizeBytes = outputBytes.Length
            };

        public static ProcessingResult Failure(string errorMessage) =>
            new ProcessingResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
    }
}
