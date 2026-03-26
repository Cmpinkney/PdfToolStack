using PdfToolStack.Application.DTOs;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;

namespace PdfToolStack.Application.Strategies
{
    public class DeletePagesStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.DeletePages;
        private readonly IDeletePagesProcessor _processor;

        public DeletePagesStrategy(IDeletePagesProcessor processor)
            => _processor = processor;

        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (request.PageNumbers == null || !request.PageNumbers.Any())
                    return ProcessingResult.Failure(
                        "No page numbers provided.");

                var output = await _processor.ProcessAsync(
                    request.FileBytes, request.PageNumbers);

                return ProcessingResult.Success(
                    output, request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure(
                    $"Delete pages failed: {ex.Message}");
            }
        }
    }
}