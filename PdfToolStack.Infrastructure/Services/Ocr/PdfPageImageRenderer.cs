using Docnet.Core;
using Docnet.Core.Models;
using PdfToolStack.Infrastructure.Processors;
using System.Runtime.InteropServices;
using SystemDrawing = System.Drawing;
using SystemImaging = System.Drawing.Imaging;

namespace PdfToolStack.Infrastructure.Services.Ocr
{
    internal static class PdfPageImageRenderer
    {
        public static Task<IReadOnlyList<PdfPageImage>> RenderPagesAsync(
            byte[] pdfBytes,
            int? maxPages = null,
            CancellationToken cancellationToken = default)
        {
            return Task.Run<IReadOnlyList<PdfPageImage>>(
                () => RenderPages(pdfBytes, maxPages, cancellationToken),
                cancellationToken);
        }

        public static int CountPages(byte[] pdfBytes)
        {
            using var lib = DocLib.Instance;
            using var doc = lib.GetDocReader(
                pdfBytes,
                new PageDimensions(2000, 2800));

            return doc.GetPageCount();
        }

        private static IReadOnlyList<PdfPageImage> RenderPages(
            byte[] pdfBytes,
            int? maxPages,
            CancellationToken cancellationToken)
        {
            var results = new List<PdfPageImage>();

            using var lib = DocLib.Instance;
            using var doc = lib.GetDocReader(
                pdfBytes,
                new PageDimensions(2000, 2800));

            var count = doc.GetPageCount();

            if (count == 0)
                throw new OcrProcessingException(
                    "OCR could not find any pages in this PDF.");

            var pagesToRender = maxPages.HasValue
                ? Math.Clamp(maxPages.Value, 1, count)
                : count;

            for (var i = 0; i < pagesToRender; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var pageReader = doc.GetPageReader(i);
                var width = pageReader.GetPageWidth();
                var height = pageReader.GetPageHeight();
                var raw = pageReader.GetImage();

                using var bmp = new SystemDrawing.Bitmap(
                    width,
                    height,
                    SystemImaging.PixelFormat.Format32bppArgb);

                var data = bmp.LockBits(
                    new SystemDrawing.Rectangle(0, 0, width, height),
                    SystemImaging.ImageLockMode.WriteOnly,
                    SystemImaging.PixelFormat.Format32bppArgb);

                Marshal.Copy(raw, 0, data.Scan0, raw.Length);
                bmp.UnlockBits(data);

                using var ms = new MemoryStream();
                bmp.Save(ms, SystemImaging.ImageFormat.Png);

                results.Add(new PdfPageImage(i + 1, ms.ToArray(), width, height));
            }

            return results;
        }
    }

    internal sealed record PdfPageImage(
        int PageNumber,
        byte[] Bytes,
        int Width,
        int Height);
}
