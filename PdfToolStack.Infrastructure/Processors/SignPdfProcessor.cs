using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Infrastructure.Processors
{
    public class SignPdfProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.SignPdf;

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(inputBytes);
        }

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            byte[] signatureImageBytes,
            float x,
            float y,
            float width,
            float height,
            int pageNumber,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new PdfReader(inputBytes);
                using var outputStream = new MemoryStream();
                using var stamper = new PdfStamper(reader, outputStream);

                if (pageNumber < 1 || pageNumber > reader.NumberOfPages)
                {
                    throw new ArgumentOutOfRangeException(nameof(pageNumber), $"Invalid page number: {pageNumber}");
                }

                var canvas = stamper.GetOverContent(pageNumber);
                var signatureImage = Image.GetInstance(signatureImageBytes);

                signatureImage.ScaleToFit(width, height);
                signatureImage.SetAbsolutePosition(x, y);

                canvas.AddImage(signatureImage);

                stamper.Close();
                return Task.FromResult(outputStream.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception($"Sign PDF failed: {ex.Message}", ex);
            }
        }
    }
}