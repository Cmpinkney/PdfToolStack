using PdfToolStack.Application.DTOs;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;

namespace PdfToolStack.Application.Strategies
{
    public class ExcelToPdfStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.ExcelToPdf;
        private readonly IPdfProcessor _processor;

        public ExcelToPdfStrategy(IPdfProcessor processor)
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
                return ProcessingResult.Failure($"Excel to PDF failed: {ex.Message}");
            }
        }
    }
}