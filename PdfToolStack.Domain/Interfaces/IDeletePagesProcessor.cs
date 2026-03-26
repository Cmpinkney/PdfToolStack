namespace PdfToolStack.Domain.Interfaces
{
    public interface IDeletePagesProcessor : IPdfProcessor
    {
        Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            IEnumerable<int> pageNumbers);
    }
}