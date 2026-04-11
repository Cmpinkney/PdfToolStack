using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfToolStack.Domain.DTOs;

namespace PdfToolStack.Infrastructure.Services;

public static class PdfAnnotationService
{
    public static byte[] Apply(byte[] pdfBytes, List<AnnotationDto> annotations)
    {
        using var inputStream = new MemoryStream(pdfBytes);
        var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

        for (int i = 0; i < document.PageCount; i++)
        {
            var page = document.Pages[i];
            var pageAnnotations = annotations.Where(a => a.Page == i + 1).ToList();
            if (!pageAnnotations.Any()) continue;

            using var gfx = XGraphics.FromPdfPage(page);
            double pageHeight = page.Height.Point;

            foreach (var ann in pageAnnotations)
            {
                var (r, g, b) = HexToRgb(ann.Color);

                switch (ann.Type)
                {
                    case "highlight":
                        {
                            double pdfX = ann.X;
                            double pdfY = ann.Y; // Use Y directly, no flip
                            var brush = new XSolidBrush(XColor.FromArgb(100, r, g, b));
                            gfx.DrawRectangle(brush, new XRect(pdfX, pdfY, ann.Width, ann.Height));
                        }
                        break;

                    case "rect":
                        {
                            double pdfX = ann.X;
                            double pdfY = ann.Y;
                            var pen = new XPen(XColor.FromArgb(r, g, b), 1.5);
                            gfx.DrawRectangle(pen, new XRect(pdfX, pdfY, ann.Width, ann.Height));
                        }
                        break;

                    case "text":
                        if (!string.IsNullOrWhiteSpace(ann.Text))
                        {
                            double pdfX = ann.X;
                            double pdfY = ann.Y;
                            var font = new XFont("Arial", ann.FontSize, XFontStyle.Regular);
                            var brush = new XSolidBrush(XColor.FromArgb(r, g, b));
                            gfx.DrawString(ann.Text, font, brush, new XPoint(pdfX, pdfY));
                        }
                        break;

                    case "ink":
                        if (ann.Points != null && ann.Points.Count > 1)
                        {
                            var pen = new XPen(XColor.FromArgb(r, g, b), 1.5);
                            for (int p = 1; p < ann.Points.Count; p++)
                            {
                                gfx.DrawLine(pen,
                                    new XPoint(ann.Points[p - 1].X, ann.Points[p - 1].Y),
                                    new XPoint(ann.Points[p].X, ann.Points[p].Y));
                            }
                        }
                        break;
                }
            }
        }

        using var outputStream = new MemoryStream();
        document.Save(outputStream);
        return outputStream.ToArray();
    }

    private static (int R, int G, int B) HexToRgb(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return (255, 0, 0);
        return (
            Convert.ToInt32(hex.Substring(0, 2), 16),
            Convert.ToInt32(hex.Substring(2, 2), 16),
            Convert.ToInt32(hex.Substring(4, 2), 16)
        );
    }
}