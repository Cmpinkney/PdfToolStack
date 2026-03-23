using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfToolkit.Domain.Interfaces;
using PdfToolkit.Domain.Enums;
using PdfToolkit.Domain.Entities;

namespace PdfToolkit.Infrastructure.Processors
{
    public class EditPdfProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.EditPdf;

        public Task<byte[]> ProcessAsync(
    byte[] inputBytes,
    CancellationToken cancellationToken = default)
        {
            return Task.FromResult(inputBytes);
        }

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            IEnumerable<PdfAnnotation> annotations,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new PdfReader(inputBytes);
                using var outputStream = new MemoryStream();
                using var stamper = new PdfStamper(
                    reader, outputStream);

                foreach (var annotation in annotations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var canvas = stamper
                        .GetOverContent(annotation.PageNumber);

                    switch (annotation.Type)
                    {
                        case "text":
                            AddText(canvas, annotation);
                            break;
                        case "rectangle":
                            AddRectangle(canvas, annotation);
                            break;
                        case "circle":
                            AddCircle(canvas, annotation);
                            break;
                        case "line":
                            AddLine(canvas, annotation);
                            break;
                    }
                }

                stamper.Close();
                return Task.FromResult(outputStream.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Edit PDF failed: {ex.Message}");
            }
        }

        private void AddText(PdfContentByte canvas,
            PdfAnnotation annotation)
        {
            var baseFont = BaseFont.CreateFont(
                BaseFont.HELVETICA,
                BaseFont.CP1252,
                BaseFont.NOT_EMBEDDED);

            canvas.BeginText();
            canvas.SetFontAndSize(baseFont,
                annotation.FontSize);
            canvas.SetColorFill(annotation.Color);
            canvas.SetTextMatrix(annotation.X, annotation.Y);
            canvas.ShowText(annotation.Text ?? "");
            canvas.EndText();
        }

        private void AddRectangle(PdfContentByte canvas,
            PdfAnnotation annotation)
        {
            canvas.SetColorStroke(annotation.Color);
            canvas.SetLineWidth(annotation.LineWidth);
            canvas.Rectangle(
                annotation.X, annotation.Y,
                annotation.Width, annotation.Height);
            canvas.Stroke();
        }

        private void AddCircle(PdfContentByte canvas,
            PdfAnnotation annotation)
        {
            canvas.SetColorStroke(annotation.Color);
            canvas.SetLineWidth(annotation.LineWidth);
            canvas.Ellipse(
                annotation.X, annotation.Y,
                annotation.X + annotation.Width,
                annotation.Y + annotation.Height);
            canvas.Stroke();
        }

        private void AddLine(PdfContentByte canvas,
            PdfAnnotation annotation)
        {
            canvas.SetColorStroke(annotation.Color);
            canvas.SetLineWidth(annotation.LineWidth);
            canvas.MoveTo(annotation.X, annotation.Y);
            canvas.LineTo(
                annotation.X2, annotation.Y2);
            canvas.Stroke();
        }
    }

    public class PdfAnnotation
    {
        public string Type { get; set; } = "text";
        public int PageNumber { get; set; } = 1;
        public float X { get; set; }
        public float Y { get; set; }
        public float X2 { get; set; }
        public float Y2 { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string? Text { get; set; }
        public float FontSize { get; set; } = 12f;
        public float LineWidth { get; set; } = 1f;
        public BaseColor Color { get; set; } = new BaseColor(0, 0, 0);
    }
}
