using Docnet.Core;
using Docnet.Core.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using Tesseract;
using SystemDrawing = System.Drawing;
using SystemImaging = System.Drawing.Imaging;

namespace PdfToolStack.Infrastructure.Processors
{
    public class PdfOcrProcessor
    {
        private readonly string _tessDataPath;

        public PdfOcrProcessor(string tessDataPath)
        {
            _tessDataPath = tessDataPath;
        }

        public async Task<byte[]> ProcessAsync(
            byte[] pdfBytes,
            string language = "eng",
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var images = RenderPages(pdfBytes, cancellationToken);

                using var engine = new TesseractEngine(
                    _tessDataPath, language, EngineMode.Default);

                using var outputStream = new MemoryStream();
                using var doc = new Document();
                var writer = PdfWriter.GetInstance(doc, outputStream);
                doc.Open();

                foreach (var (imageBytes, width, height) in images)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var ms = new MemoryStream(imageBytes);
                    using var pix = Pix.LoadFromMemory(imageBytes);
                    using var page = engine.Process(pix);

                    var text = page.GetText();

                    doc.SetPageSize(new iTextSharp.text.Rectangle(width, height));
                    doc.NewPage();

                    // Add hidden text layer for searchability
                    var cb = writer.DirectContent;
                    cb.BeginText();
                    cb.SetFontAndSize(
                        BaseFont.CreateFont(
                            BaseFont.HELVETICA,
                            BaseFont.WINANSI,
                            BaseFont.NOT_EMBEDDED),
                        10);
                    cb.SetTextRenderingMode(3); // invisible
                    cb.ShowTextAligned(
                        Element.ALIGN_LEFT, text, 0, 0, 0);
                    cb.EndText();

                    // Add visible image on top
                    var img = iTextSharp.text.Image
                        .GetInstance(imageBytes);
                    img.SetAbsolutePosition(0, 0);
                    img.ScaleToFit(width, height);
                    doc.Add(img);
                }

                doc.Close();
                return outputStream.ToArray();

            }, cancellationToken);
        }

        public string ExtractText(
            byte[] pdfBytes,
            string language = "eng",
            CancellationToken cancellationToken = default)
        {
            var images = RenderPages(pdfBytes, cancellationToken);
            var sb = new System.Text.StringBuilder();

            using var engine = new TesseractEngine(
                _tessDataPath, language, EngineMode.Default);

            foreach (var (imageBytes, _, _) in images)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var pix = Pix.LoadFromMemory(imageBytes);
                using var page = engine.Process(pix);
                sb.AppendLine(page.GetText());
            }

            return sb.ToString();
        }

        private static List<(byte[] Bytes, int Width, int Height)>
            RenderPages(byte[] pdfBytes,
                CancellationToken cancellationToken)
        {
            var results = new List<(byte[], int, int)>();

            using var lib = DocLib.Instance;
            using var doc = lib.GetDocReader(
                pdfBytes, new PageDimensions(2000, 2800));

            var count = doc.GetPageCount();

            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var pageReader = doc.GetPageReader(i);
                var w = pageReader.GetPageWidth();
                var h = pageReader.GetPageHeight();
                var raw = pageReader.GetImage();

                using var bmp = new SystemDrawing.Bitmap(w, h,
     SystemImaging.PixelFormat.Format32bppArgb);
                var data = bmp.LockBits(
                    new SystemDrawing.Rectangle(0, 0, w, h),
                    SystemImaging.ImageLockMode.WriteOnly,
                    SystemImaging.PixelFormat.Format32bppArgb);
                Marshal.Copy(raw, 0, data.Scan0, raw.Length);
                bmp.UnlockBits(data);

                using var ms = new MemoryStream();
                bmp.Save(ms, SystemImaging.ImageFormat.Png);
            }

            return results;
        }
    }
}