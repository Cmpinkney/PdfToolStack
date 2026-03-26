using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Entities;

namespace PdfToolStack.Infrastructure.Processors
{
    public class AnnotatePdfProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.AnnotatePdf;

        public Task<byte[]> ProcessAsync(
    byte[] inputBytes,
    CancellationToken cancellationToken = default)
        {
            return Task.FromResult(inputBytes);
        }

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            IEnumerable<PdfHighlight> highlights,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new PdfReader(inputBytes);
                using var outputStream = new MemoryStream();
                using var stamper = new PdfStamper(
                    reader, outputStream);

                foreach (var highlight in highlights)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var canvas = stamper
                        .GetOverContent(highlight.PageNumber);

                    switch (highlight.Type)
                    {
                        case "highlight":
                            canvas.SaveState();
                            canvas.SetColorFill(
                                new BaseColor(255, 255, 0, 100));
                            canvas.Rectangle(
                                highlight.X, highlight.Y,
                                highlight.Width, highlight.Height);
                            canvas.Fill();
                            canvas.RestoreState();
                            break;

                        case "underline":
                            canvas.SetColorStroke(
                                new BaseColor(0, 0, 255));
                            canvas.SetLineWidth(1.5f);
                            canvas.MoveTo(
                                highlight.X, highlight.Y);
                            canvas.LineTo(
                                highlight.X + highlight.Width,
                                highlight.Y);
                            canvas.Stroke();
                            break;

                        case "strikethrough":
                            canvas.SetColorStroke(
                                new BaseColor(255, 0, 0));
                            canvas.SetLineWidth(1.5f);
                            var midY = highlight.Y +
                                (highlight.Height / 2);
                            canvas.MoveTo(highlight.X, midY);
                            canvas.LineTo(
                                highlight.X + highlight.Width,
                                midY);
                            canvas.Stroke();
                            break;

                        case "freehand":
                            if (highlight.Points?.Any() == true)
                            {
                                canvas.SetColorStroke(
                                    highlight.StrokeColor
                                    ?? new BaseColor(0, 0, 0));
                                canvas.SetLineWidth(
                                    highlight.LineWidth);
                                var first = highlight.Points[0];
                                canvas.MoveTo(first.X, first.Y);
                                foreach (var point in
                                    highlight.Points.Skip(1))
                                    canvas.LineTo(point.X, point.Y);
                                canvas.Stroke();
                            }
                            break;
                    }
                }

                stamper.Close();
                return Task.FromResult(outputStream.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Annotate PDF failed: {ex.Message}");
            }
        }
    }

    public class PdfHighlight
    {
        public string Type { get; set; } = "highlight";
        public int PageNumber { get; set; } = 1;
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float LineWidth { get; set; } = 2f;
        public BaseColor? StrokeColor { get; set; } = null;
        public List<PointF> Points { get; set; } = new();
    }

    public class PointF
    {
        public float X { get; set; }
        public float Y { get; set; }
    }
}