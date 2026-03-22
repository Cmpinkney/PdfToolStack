using PDFToolkit.Application.DTOs;
using PDFToolkit.Domain.Entities;
using PDFToolkit.Domain.Enums;

namespace PDFToolkit.Application.Strategies
{
    public interface IProcessingStrategy
    {
        ToolType ToolType { get; }
        Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default);
    }
}
