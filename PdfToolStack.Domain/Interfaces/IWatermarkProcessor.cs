using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Domain.Interfaces
{
    public interface IWatermarkProcessor : IPdfProcessor
    {
        Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            string watermarkText,
            float opacity = 0.3f,
            float fontSize = 48f,
            CancellationToken cancellationToken = default);
    }
}