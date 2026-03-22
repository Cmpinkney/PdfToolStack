using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfToolkit.Domain.Enums;
using PdfToolkit.Domain.Interfaces;

namespace PdfToolkit.Infrastructure.Processors
{
    public class PdfRedactor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.RedactPdf;

        // Redaction regions: each float[] = { x1, y1, x2, y2, pageNumber }
        private readonly IEnumerable<RedactionRegion> _regions;

        public PdfRedactor(
            IEnumerable<RedactionRegion>? regions = null)
        {
            _regions = regions
                ?? Enumerable.Empty<RedactionRegion>();
        }

        public async Task<byte[]> ProcessAsync(
            byte[] fileBytes,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                using var inputStream =
                    new MemoryStream(fileBytes);
                using var outputStream = new MemoryStream();

                var reader = new PdfReader(inputStream);
                var stamper = new PdfStamper(
                    reader, outputStream);

                foreach (var region in _regions)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    if (region.PageNumber < 1 ||
                        region.PageNumber > reader.NumberOfPages)
                        continue;

                    var canvas = stamper.GetOverContent(
                        region.PageNumber);

                    // Draw permanent black rectangle
                    canvas.SetColorFill(BaseColor.Black);
                    canvas.Rectangle(
                        region.X1,
                        region.Y1,
                        region.X2 - region.X1,
                        region.Y2 - region.Y1);
                    canvas.Fill();

                    // Draw border
                    canvas.SetColorStroke(BaseColor.Black);
                    canvas.Rectangle(
                        region.X1,
                        region.Y1,
                        region.X2 - region.X1,
                        region.Y2 - region.Y1);
                    canvas.Stroke();
                }

                // Strip metadata to prevent data leaks
                var info = reader.Info;
                info.Clear();
                stamper.MoreInfo = info;

                // Remove usage rights
                reader.RemoveUsageRights();

                stamper.Close();
                reader.Close();

                return outputStream.ToArray();

            }, cancellationToken);
        }
    }

    public class RedactionRegion
    {
        public float X1 { get; set; }
        public float Y1 { get; set; }
        public float X2 { get; set; }
        public float Y2 { get; set; }
        public int PageNumber { get; set; } = 1;
    }
}