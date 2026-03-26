using PdfToolStack.Application.DTOs;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;

namespace PdfToolStack.Application.Strategies
{
    public class RotateStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.RotatePdf;
        private readonly IPdfProcessor _processor;

        public RotateStrategy(IPdfProcessor processor)
            => _processor = processor;

        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Default 90° rotation via standard ProcessAsync
                // Advanced rotation options (specific pages, degrees)
                // can be added via ProcessRequest extension later
                var output = await _processor.ProcessAsync(
                    request.FileBytes, cancellationToken);
                return ProcessingResult.Success(output, request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure($"Rotate failed: {ex.Message}");
            }
        }
    }
}