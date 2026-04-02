using iTextSharp.text.pdf;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;

namespace PdfToolStack.Infrastructure.Processors
{
    public class PdfCompressor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.CompressPdf;

        public async Task<byte[]> ProcessAsync(
            byte[] fileBytes,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                using var inputStream = new MemoryStream(fileBytes);
                using var outputStream = new MemoryStream();

                using var reader = new PdfReader(inputStream);
                using var stamper = new PdfStamper(reader, outputStream);

                stamper.SetFullCompression();

                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var pageContent = reader.GetPageContent(i);
                    reader.SetPageContent(i, pageContent);
                }

                stamper.Close();

                var outputBytes = outputStream.ToArray();

                if (outputBytes.Length == 0)
                    throw new InvalidOperationException("Compressed PDF output was empty.");

                return outputBytes;
            }, cancellationToken);
        }
    }
}
