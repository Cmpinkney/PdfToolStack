using PdfToolkit.Application.DTOs;
using PdfToolkit.Domain.Entities;
using PdfToolkit.Domain.Enums;

namespace PdfToolkit.Application.Strategies
{
    public interface IProcessingStrategy
    {
        ToolType ToolType { get; }
        Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default);
    }
}
