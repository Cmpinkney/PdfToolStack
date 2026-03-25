using PdfToolkit.Application.DTOs;
using PdfToolkit.Domain.Entities;
using PdfToolkit.Domain.Enums;
using PdfToolkit.Domain.Interfaces;

namespace PdfToolkit.Application.Strategies
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