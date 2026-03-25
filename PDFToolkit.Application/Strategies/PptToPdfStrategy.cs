using PdfToolkit.Application.DTOs;
using PdfToolkit.Domain.Entities;
using PdfToolkit.Domain.Enums;
using PdfToolkit.Domain.Interfaces;

namespace PdfToolkit.Application.Strategies
{
    public class PptToPdfStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.PptToPdf;
        private readonly IPdfProcessor _processor;

        public PptToPdfStrategy(IPdfProcessor processor)
            => _processor = processor;

        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var output = await _processor.ProcessAsync(
                    request.FileBytes, cancellationToken);
                return ProcessingResult.Success(output, request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure($"PPT to PDF failed: {ex.Message}");
            }
        }
    }
}