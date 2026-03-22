using PDFToolkit.Application.DTOs;
using PDFToolkit.Domain.Entities;
using PDFToolkit.Domain.Enums;

namespace PDFToolkit.Application.Strategies
{
    public class RedactStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.RedactPdf;
        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (request.FileBytes == null || request.FileBytes.Length == 0)
                    return ProcessingResult.Failure("File is empty or invalid.");

                if (!IsPdf(request.FileBytes))
                    return ProcessingResult.Failure("File is not a valid PDF.");

                await Task.CompletedTask;

                return ProcessingResult.Success(
                    request.FileBytes,
                    request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure(
                    $"Redaction failed: {ex.Message}");
            }
        }

        private static bool IsPdf(byte[] bytes)
        {
            return bytes.Length >= 4 &&
                   bytes[0] == 0x25 &&
                   bytes[1] == 0x50 &&
                   bytes[2] == 0x44 &&
                   bytes[3] == 0x46;
        }
    }
}
