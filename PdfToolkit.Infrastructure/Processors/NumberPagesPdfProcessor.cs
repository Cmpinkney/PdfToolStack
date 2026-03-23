using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfToolkit.Domain.Interfaces;
using PdfToolkit.Domain.Enums;

namespace PdfToolkit.Infrastructure.Processors
{
    public class NumberPagesPdfProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.NumberPages;

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
            => Task.FromResult(inputBytes);

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            string position = "bottom-center",
            int startNumber = 1,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new PdfReader(inputBytes);
                using var outputStream = new MemoryStream();
                using var stamper = new PdfStamper(
                    reader, outputStream);

                var baseFont = BaseFont.CreateFont(
                    BaseFont.HELVETICA,
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
                    var pageNum = i + startNumber - 1;

                    float x = position switch
                    {
                        "bottom-left" => 30f,
                        "bottom-right" =>
                            pageSize.Width - 30f,
                        _ => pageSize.Width / 2
                    };
                    float y = position
                        .StartsWith("top") ?
                            pageSize.Height - 30f : 30f;

                    canvas.BeginText();
                    canvas.SetFontAndSize(baseFont, 10f);
                    canvas.SetColorFill(
                        new BaseColor(0, 0, 0));
                    canvas.ShowTextAligned(
                        Element.ALIGN_CENTER,
                        pageNum.ToString(),
                        x, y, 0f);
                    canvas.EndText();
                }

                stamper.Close();
                return Task.FromResult(
                    outputStream.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Number pages failed: {ex.Message}");
            }
        }
    }
}