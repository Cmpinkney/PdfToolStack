using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfToolkit.Domain.Interfaces;
using PdfToolkit.Domain.Enums;

namespace PdfToolkit.Infrastructure.Processors
{
    public class SplitPdfProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.SplitPdf;

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
            => Task.FromResult(inputBytes);

        public Task<List<byte[]>> SplitAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new PdfReader(inputBytes);
                int totalPages = reader.NumberOfPages;
                var result = new List<byte[]>();

                for (int i = 1; i <= totalPages; i++)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();
                    using var outputStream = new MemoryStream();
                    using var doc = new Document(
                        reader.GetPageSizeWithRotation(i));
                    using var copy = new PdfCopy(
                        doc, outputStream);
                    doc.Open();
                    copy.AddPage(copy.GetImportedPage(
                        reader, i));
                    doc.Close();
                    result.Add(outputStream.ToArray());
                }

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Split PDF failed: {ex.Message}");
            }
        }

        public Task<byte[]> SplitRangeAsync(
            byte[] inputBytes,
            int fromPage,
            int toPage,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new PdfReader(inputBytes);
                using var outputStream = new MemoryStream();
                using var doc = new Document(
                    reader.GetPageSizeWithRotation(fromPage));
                using var copy = new PdfCopy(
                    doc, outputStream);
                doc.Open();
                for (int i = fromPage; i <= toPage; i++)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();
                    copy.AddPage(copy.GetImportedPage(
                        reader, i));
                }
                doc.Close();
                return Task.FromResult(outputStream.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Split range failed: {ex.Message}");
            }
        }
    }
}