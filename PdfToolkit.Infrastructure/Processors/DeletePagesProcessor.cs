using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfToolkit.Domain.Interfaces;
using PdfToolkit.Domain.Enums;
using PdfToolkit.Domain.Entities;

namespace PdfToolkit.Infrastructure.Processors
{
    public class DeletePagesProcessor : IDeletePagesProcessor
    {
        public ToolType ToolType => ToolType.DeletePages;

        // Standard IPdfProcessor passthrough
        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
            => Task.FromResult(inputBytes);

        // Explicit interface implementation — called by strategy
        Task<byte[]> IDeletePagesProcessor.ProcessAsync(
            byte[] inputBytes,
            IEnumerable<int> pageNumbers)
            => ProcessAsync(inputBytes, pageNumbers, default);

        // Full implementation with cancellation support
        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            IEnumerable<int> pagesToDelete,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new PdfReader(inputBytes);
                int totalPages = reader.NumberOfPages;

                var deleteSet = new HashSet<int>(pagesToDelete);
                var keepPages = Enumerable.Range(1, totalPages)
                    .Where(p => !deleteSet.Contains(p))
                    .ToList();

                if (keepPages.Count == 0)
                    throw new Exception(
                        "Cannot delete all pages from a PDF.");

                using var outputStream = new MemoryStream();
                using var doc = new Document(
                    reader.GetPageSizeWithRotation(1));
                using var copy = new PdfCopy(doc, outputStream);

                doc.Open();
                foreach (var pageNum in keepPages)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    copy.AddPage(copy.GetImportedPage(
                        reader, pageNum));
                }
                doc.Close();

                return Task.FromResult(outputStream.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Delete pages failed: {ex.Message}");
            }
        }
    }
}