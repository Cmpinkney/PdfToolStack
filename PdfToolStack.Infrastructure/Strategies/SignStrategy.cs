using PdfToolStack.Application.DTOs;
using PdfToolStack.Application.Strategies;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Infrastructure.Strategies
{
    public class SignStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.SignPdf;

        private readonly Processors.SignPdfProcessor _processor;

        public SignStrategy(Processors.SignPdfProcessor processor)
            => _processor = processor;

        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var output = await _processor.ProcessAsync(
                    request.FileBytes,
                    request.SignatureBytes,
                    request.SignatureX,
                    request.SignatureY,
                    request.SignatureWidth,
                    request.SignatureHeight,
                    request.SignaturePageNumber,
                    cancellationToken);

                return ProcessingResult.Success(output, request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure($"Sign failed: {ex.Message}");
            }
        }
    }
}