using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Domain.Enums;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using iTextDocument = iTextSharp.text.Document;
using iTextPageSize = iTextSharp.text.PageSize;
using iTextParagraph = iTextSharp.text.Paragraph;
using iTextFont = iTextSharp.text.Font;
using iTextBaseFont = iTextSharp.text.pdf.BaseFont;
using iTextPdfWriter = iTextSharp.text.pdf.PdfWriter;
using DrawingText = DocumentFormat.OpenXml.Drawing.Text;

namespace PdfToolStack.Infrastructure.Processors
{
    public class PptToPdfProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.PptToPdf;

        public async Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                using var pptStream =
                    new MemoryStream(inputBytes);
                using var pptDoc =
                    PresentationDocument.Open(
                        pptStream, false);

                var presentationPart =
                    pptDoc.PresentationPart;
                if (presentationPart == null)
                    throw new Exception(
                        "Could not read presentation.");

                var slides = presentationPart
                    .SlideParts.ToList();

                using var outputStream =
                    new MemoryStream();
                using var doc = new iTextDocument(
                    iTextPageSize.A4.Rotate(),
                    30f, 30f, 30f, 30f);

                iTextPdfWriter.GetInstance(
                    doc, outputStream);
                doc.Open();

                var baseFont = iTextBaseFont.CreateFont(
                    iTextBaseFont.HELVETICA,
                    iTextBaseFont.CP1252,
                    iTextBaseFont.NOT_EMBEDDED);

                bool firstSlide = true;
                foreach (var slide in slides)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    if (!firstSlide) doc.NewPage();
                    firstSlide = false;

                    var texts = slide.Slide
                        .Descendants<DrawingText>()
                        .Select(t => t.Text)
                        .Where(t =>
                            !string.IsNullOrWhiteSpace(t))
                        .ToList();

                    if (texts.Any())
                    {
                        var title = texts.First();
                        doc.Add(new iTextParagraph(title,
                            new iTextFont(baseFont, 20f,
                                iTextFont.BOLD))
                        { SpacingAfter = 12f });

                        foreach (var text in texts.Skip(1))
                        {
                            doc.Add(new iTextParagraph(
                                text,
                                new iTextFont(baseFont, 12f))
                            { SpacingAfter = 6f });
                        }
                    }
                    else
                    {
                        doc.Add(new iTextParagraph(
                            "[Slide with no text content]",
                            new iTextFont(baseFont, 10f,
                                iTextFont.ITALIC)));
                    }
                }

                doc.Close();
                return outputStream.ToArray();

            }, cancellationToken);
        }
    }
}
