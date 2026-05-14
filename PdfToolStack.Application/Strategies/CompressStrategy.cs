using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Application.DTOs;

namespace PdfToolStack.Application.Strategies
{
    public class CompressStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.CompressPdf;

        private readonly ICompressProcessor _processor;

        public CompressStrategy(ICompressProcessor processor)
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
                    request.FileBytes,
                    request.CompressionProfile,
                    cancellationToken);

                return ProcessingResult.Success(
                    outputBytes,
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
            return bytes.Length >= 4 &&
                   bytes[0] == 0x25 &&
                   bytes[1] == 0x50 &&
                   bytes[2] == 0x44 &&
                   bytes[3] == 0x46;
        }
    }
}