using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Entities;

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
            float x, float y,
            float width, float height,
            int pageNumber,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new PdfReader(inputBytes);
                using var outputStream = new MemoryStream();
                using var stamper = new PdfStamper(
                    reader, outputStream);

                var canvas = stamper.GetOverContent(pageNumber);
                var signatureImage = Image.GetInstance(
                    signatureImageBytes);
                signatureImage.SetAbsolutePosition(x, y);
                signatureImage.ScaleToFit(width, height);
                canvas.AddImage(signatureImage);
                stamper.Close();

                return Task.FromResult(outputStream.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Sign PDF failed: {ex.Message}");
            }
        }
    }
}