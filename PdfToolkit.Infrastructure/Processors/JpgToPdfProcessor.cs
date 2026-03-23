using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfToolkit.Domain.Interfaces;
using PdfToolkit.Domain.Enums;

namespace PdfToolkit.Infrastructure.Processors
{
    public class JpgToPdfProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.JpgToPdf;

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
            => Task.FromResult(inputBytes);

        public Task<byte[]> ProcessAsync(
            IEnumerable<byte[]> imageFiles,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var outputStream = new MemoryStream();
                using var doc = new Document(
                    PageSize.A4, 0, 0, 0, 0);
                PdfWriter.GetInstance(doc, outputStream);
                doc.Open();

                foreach (var imageBytes in imageFiles)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    var img = Image.GetInstance(imageBytes);
                    img.ScaleToFit(
                        PageSize.A4.Width,
                        PageSize.A4.Height);
                    img.SetAbsolutePosition(
                        (PageSize.A4.Width - img.ScaledWidth)
                            / 2,
                        (PageSize.A4.Height - img.ScaledHeight)
                            / 2);
                    doc.NewPage();
                    doc.Add(img);
                }

                doc.Close();
                return Task.FromResult(
                    outputStream.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"JPG to PDF failed: {ex.Message}");
            }
        }
    }
}