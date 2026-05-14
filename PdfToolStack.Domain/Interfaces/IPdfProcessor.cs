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

    public interface ICompressProcessor : IPdfProcessor
    {
        Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CompressionProfile profile,
            CancellationToken cancellationToken = default);
    }

    public interface IProtectPdfProcessor : IPdfProcessor
    {
        Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            string userPassword,
            string ownerPassword,
            bool allowPrinting = true,
            bool allowCopying = false,
            CancellationToken cancellationToken = default);
    }
}
