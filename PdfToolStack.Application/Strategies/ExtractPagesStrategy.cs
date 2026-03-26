using PdfToolStack.Application.DTOs;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;

namespace PdfToolStack.Application.Strategies
{
    public class ExtractPagesStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.ExtractPages;
        private readonly IExtractPagesProcessor _processor;

        public ExtractPagesStrategy(IExtractPagesProcessor processor)
            => _processor = processor;

        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var pages = request.PageNumbers
                    ?? new List<int> { 1 };

                var output = await _processor.ProcessAsync(
                    request.FileBytes, pages);

                return ProcessingResult.Success(
                    output, request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure(
                    $"Extract pages failed: {ex.Message}");
            }
        }
    }
}