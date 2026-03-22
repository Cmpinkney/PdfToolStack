using iTextSharp.text.pdf;
using PdfToolkit.Domain.Enums;
using PdfToolkit.Domain.Interfaces;

namespace PdfToolkit.Infrastructure.Processors
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

                var reader = new PdfReader(inputStream);
                var stamper = new PdfStamper(reader, outputStream);

                // Compress content streams
                stamper.SetFullCompression();

                // Compress each page
                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    reader.SetPageContent(i, reader.GetPageContent(i));
                }

                stamper.Close();
                reader.Close();

                return outputStream.ToArray();

            }, cancellationToken);
        }
    }
}
