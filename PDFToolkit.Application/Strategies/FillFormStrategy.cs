using PdfToolkit.Domain.Entities;
using PdfToolkit.Domain.Enums;
using PdfToolkit.Domain.Interfaces;
using PdfToolkit.Application.DTOs;

namespace PdfToolkit.Application.Strategies
{
    public class FillFormStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.FillPdfForm;

        private readonly IPdfProcessor _processor;

        public FillFormStrategy(IPdfProcessor processor)
        {
            _processor = processor;
        }

        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (request.FileBytes == null
                    || request.FileBytes.Length == 0)
                    return ProcessingResult.Failure(
                        "File is empty or invalid.");

                if (!IsPdf(request.FileBytes))
                    return ProcessingResult.Failure(
                        "File is not a valid PDF.");

                var outputBytes = await _processor.ProcessAsync(
                    request.FileBytes, cancellationToken);

                return ProcessingResult.Success(
                    outputBytes,
                    request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure(
                    $"Form fill failed: {ex.Message}");
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
