using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfToolkit.Domain.Interfaces;
using PdfToolkit.Domain.Enums;

namespace PdfToolkit.Infrastructure.Processors
{
    public class ExtractPagesProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.ExtractPages;

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
            => Task.FromResult(inputBytes);

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            IEnumerable<int> pagesToExtract,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new PdfReader(inputBytes);
                using var outputStream = new MemoryStream();
                using var doc = new Document(
                    reader.GetPageSizeWithRotation(1));
                using var copy = new PdfCopy(
                    doc, outputStream);
                doc.Open();
                foreach (var pageNum in pagesToExtract
                    .OrderBy(p => p))
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();
                    copy.AddPage(copy.GetImportedPage(
                        reader, pageNum));
                }
                doc.Close();
                return Task.FromResult(
                    outputStream.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Extract pages failed: {ex.Message}");
            }
        }
    }
}