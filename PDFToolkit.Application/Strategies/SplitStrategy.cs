using PdfToolkit.Application.DTOs;
using PdfToolkit.Domain.Entities;
using PdfToolkit.Domain.Enums;
using PdfToolkit.Domain.Interfaces;

namespace PdfToolkit.Application.Strategies
{
    public class SplitStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.SplitPdf;
        private readonly IPdfProcessor _processor;

        public SplitStrategy(IPdfProcessor processor)
            => _processor = processor;

        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // ProcessAsync returns first page for now
                // TODO: return zip of all pages
                var output = await _processor.ProcessAsync(
                    request.FileBytes, cancellationToken);
                return ProcessingResult.Success(output, request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure($"Split failed: {ex.Message}");
            }
        }
    }
}