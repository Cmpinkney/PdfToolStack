using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Domain.Interfaces
{
    public interface IPdfProcessor
    {
        ToolType ToolType { get; }
        Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default);
    }
}