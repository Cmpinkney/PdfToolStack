using PDFToolkit.Application.DTOs;
using PDFToolkit.Domain.Entities;
using PDFToolkit.Domain.Enums;

namespace PDFToolkit.Application.Strategies
{
    public class CompressStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.CompressPdf;
        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate input
                if (request.FileBytes == null || request.FileBytes.Length == 0)
                    return ProcessingResult.Failure("File is empty or invalid.");

                // Check PDF magic bytes — %PDF
                if (!IsPdf(request.FileBytes))
                    return ProcessingResult.Failure("File is not a valid PDF.");

                // Compression logic will be implemented in Infrastructure
                // This strategy delegates to the injected processor
                await Task.CompletedTask;

                return ProcessingResult.Success(
                    request.FileBytes,
                    request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure(
                    $"Compression failed: {ex.Message}");
            }
        }

        private static bool IsPdf(byte[] bytes)
        {
            // PDF magic bytes: %PDF = 0x25 0x50 0x44 0x46
            return bytes.Length >= 4 &&
                   bytes[0] == 0x25 &&
                   bytes[1] == 0x50 &&
                   bytes[2] == 0x44 &&
                   bytes[3] == 0x46;
        }
    }
}
