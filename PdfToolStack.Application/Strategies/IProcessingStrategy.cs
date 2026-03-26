using PdfToolStack.Application.DTOs;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Application.Strategies
{
    public interface IProcessingStrategy
    {
        ToolType ToolType { get; }
        Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default);
    }
}
