using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;

namespace PdfToolStack.Infrastructure.Processors
{
    public class CropPdfProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.CropPdf;

        // Required by IPdfProcessor — not used for crop (needs margin params)
        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(inputBytes);
        }

        /// <summary>
        /// Crops all pages (or a specific set) by applying margin offsets.
        /// Margins are in PDF points (1pt = 1/72 inch).
        /// The CropBox is set relative to the existing MediaBox.
        /// </summary>
        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            float marginTop,
            float marginRight,
            float marginBottom,
            float marginLeft,
            List<int>? pageNumbers = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new PdfReader(inputBytes);
                using var outputStream = new MemoryStream();
                using var stamper = new PdfStamper(reader, outputStream);

                var totalPages = reader.NumberOfPages;

                // null = apply to all pages
                var targetPages = pageNumbers?.Where(p => p >= 1 && p <= totalPages).ToHashSet()
                    ?? Enumerable.Range(1, totalPages).ToHashSet();

                for (int i = 1; i <= totalPages; i++)
                {
                    if (!targetPages.Contains(i)) continue;

                    // Use the existing MediaBox as the base
                    var mediaBox = reader.GetPageSize(i);

                    // Clamp margins so we can't crop to zero/negative size
                    var left = mediaBox.Left + Math.Max(0, marginLeft);
                    var bottom = mediaBox.Bottom + Math.Max(0, marginBottom);
                    var right = mediaBox.Right - Math.Max(0, marginRight);
                    var top = mediaBox.Top - Math.Max(0, marginTop);

                    // Guard: don't allow inverted rectangles
                    if (right <= left || top <= bottom)
                        continue;

                    var cropBox = new Rectangle(left, bottom, right, top);

                    var pageDict = reader.GetPageN(i);
                    pageDict.Put(PdfName.Cropbox, new PdfRectangle(cropBox));
                }

                stamper.Close();
                return Task.FromResult(outputStream.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception($"Crop PDF failed: {ex.Message}", ex);
            }
        }
    }
}
