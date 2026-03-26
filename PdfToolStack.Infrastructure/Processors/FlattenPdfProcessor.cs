using iTextSharp.text.pdf;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Infrastructure.Processors
{
    public class FlattenPdfProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.FlattenPdf;

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new PdfReader(inputBytes);
                using var outputStream = new MemoryStream();
                using var stamper = new PdfStamper(
                    reader, outputStream);
                stamper.FormFlattening = true;
                stamper.FreeTextFlattening = true;
                stamper.Close();
                return Task.FromResult(
                    outputStream.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Flatten PDF failed: {ex.Message}");
            }
        }
    }
}