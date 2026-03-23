using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfToolkit.Domain.Interfaces;
using PdfToolkit.Domain.Enums;

namespace PdfToolkit.Infrastructure.Processors
{
    public class WatermarkPdfProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.WatermarkPdf;

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
            => Task.FromResult(inputBytes);

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            string watermarkText,
            float opacity = 0.3f,
            float fontSize = 48f,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new PdfReader(inputBytes);
                using var outputStream = new MemoryStream();
                using var stamper = new PdfStamper(
                    reader, outputStream);

                var baseFont = BaseFont.CreateFont(
                    BaseFont.HELVETICA_BOLD,
                    BaseFont.CP1252,
                    BaseFont.NOT_EMBEDDED);

                for (int i = 1;
                    i <= reader.NumberOfPages; i++)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    var pageSize = reader
                        .GetPageSizeWithRotation(i);
                    var canvas = stamper
                        .GetOverContent(i);

                    canvas.SaveState();
                    var gState = new PdfGState
                    {
                        FillOpacity = opacity,
                        StrokeOpacity = opacity
                    };
                    canvas.SetGState(gState);
                    canvas.SetColorFill(
                        new BaseColor(128, 128, 128));
                    canvas.BeginText();
                    canvas.SetFontAndSize(
                        baseFont, fontSize);
                    canvas.ShowTextAligned(
                        Element.ALIGN_CENTER,
                        watermarkText,
                        pageSize.Width / 2,
                        pageSize.Height / 2,
                        45f);
                    canvas.EndText();
                    canvas.RestoreState();
                }

                stamper.Close();
                return Task.FromResult(
                    outputStream.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Watermark failed: {ex.Message}");
            }
        }
    }
}