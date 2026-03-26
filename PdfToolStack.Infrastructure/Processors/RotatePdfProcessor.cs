using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Infrastructure.Processors
{
    public class RotatePdfProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.RotatePdf;

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
            => Task.FromResult(inputBytes);

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            int rotation,
            IEnumerable<int>? specificPages = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new PdfReader(inputBytes);
                int totalPages = reader.NumberOfPages;
                var pagesToRotate = specificPages?.ToHashSet()
                    ?? Enumerable.Range(1, totalPages)
                        .ToHashSet();

                for (int i = 1; i <= totalPages; i++)
                {
                    if (!pagesToRotate.Contains(i)) continue;
                    var dict = reader.GetPageN(i);
                    var current = dict
                        .GetAsNumber(PdfName.Rotate)
                        ?.IntValue ?? 0;
                    dict.Put(PdfName.Rotate,
                        new PdfNumber(
                            (current + rotation) % 360));
                }

                using var outputStream = new MemoryStream();
                using var stamper = new PdfStamper(
                    reader, outputStream);
                stamper.Close();
                return Task.FromResult(
                    outputStream.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Rotate PDF failed: {ex.Message}");
            }
        }
    }
}