using PdfToolkit.Domain.Enums;

namespace PdfToolkit.Domain.Interfaces
{
    public interface IPdfProcessor
    {
        ToolType ToolType { get; }
        Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default);
    }
}