using PDFToolkit.Application.DTOs;
using PDFToolkit.Domain.Entities;
using PDFToolkit.Domain.Enums;

namespace PDFToolkit.Application.Strategies
{
    public class MergeStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.MergePdf;
        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (request.FileBytes == null || request.FileBytes.Length == 0)
                    return ProcessingResult.Failure("File is empty or invalid.");

                await Task.CompletedTask;

                return ProcessingResult.Success(
                    request.FileBytes,
                    request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure(
                    $"Merge failed: {ex.Message}");
            }
        }
    }
}
