namespace PdfToolkit.Domain.Interfaces
{
    public interface IExtractPagesProcessor : IPdfProcessor
    {
        Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            IEnumerable<int> pageNumbers);
    }
}