using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iTextSharp.text.pdf;
using PdfToolkit.Domain.Interfaces;
using PdfToolkit.Domain.Enums;

// Aliases to resolve namespace conflicts
using iTextDocument = iTextSharp.text.Document;
using iTextPageSize = iTextSharp.text.PageSize;
using iTextParagraph = iTextSharp.text.Paragraph;
using iTextFont = iTextSharp.text.Font;
using iTextBaseFont = iTextSharp.text.pdf.BaseFont;
using iTextPTable = iTextSharp.text.pdf.PdfPTable;
using iTextPCell = iTextSharp.text.pdf.PdfPCell;
using iTextPhrase = iTextSharp.text.Phrase;

namespace PdfToolkit.Infrastructure.Processors
{
    public class WordToPdfProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.WordToPdf;

        public async Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                using var wordStream =
                    new MemoryStream(inputBytes);
                using var wordDoc =
                    WordprocessingDocument.Open(
                        wordStream, false);

                var body = wordDoc.MainDocumentPart?
                    .Document?.Body;

                if (body == null)
                    throw new Exception(
                        "Could not read Word document.");

                using var outputStream = new MemoryStream();
                using var doc = new iTextDocument(
                    iTextPageSize.A4, 50f, 50f, 50f, 50f);

                PdfWriter.GetInstance(doc, outputStream);
                doc.Open();

                var baseFont = iTextBaseFont.CreateFont(
                    iTextBaseFont.HELVETICA,
                    iTextBaseFont.CP1252,
                    iTextBaseFont.NOT_EMBEDDED);

                foreach (var element in body.Elements())
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    if (element is Paragraph para)
                    {
                        var text = para.InnerText;
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            doc.Add(new iTextParagraph(
                                " ", new iTextFont(
                                    baseFont, 6f)));
                            continue;
                        }

                        var style = para
                            .ParagraphProperties?
                            .ParagraphStyleId?
                            .Val?.Value ?? "";

                        float fontSize = 11f;
                        int fontStyle = iTextFont.NORMAL;

                        if (style.StartsWith("Heading1"))
                        {
                            fontSize = 18f;
                            fontStyle = iTextFont.BOLD;
                        }
                        else if (style.StartsWith("Heading2"))
                        {
                            fontSize = 15f;
                            fontStyle = iTextFont.BOLD;
                        }
                        else if (style.StartsWith("Heading3"))
                        {
                            fontSize = 13f;
                            fontStyle = iTextFont.BOLD;
                        }

                        var font = new iTextFont(
                            baseFont, fontSize, fontStyle);
                        var pdfPara = new iTextParagraph(
                            text, font)
                        {
                            SpacingAfter = 8f
                        };
                        doc.Add(pdfPara);
                    }
                    else if (element is Table table)
                    {
                        var colCount = table
                            .Elements<TableRow>()
                            .FirstOrDefault()
                            ?.Elements<TableCell>()
                            .Count() ?? 1;

                        var pdfTable = new iTextPTable(colCount)
                        {
                            WidthPercentage = 100f,
                            SpacingAfter = 10f
                        };

                        foreach (var row in
                            table.Elements<TableRow>())
                        {
                            foreach (var cell in
                                row.Elements<TableCell>())
                            {
                                var cellFont = new iTextFont(
                                    baseFont, 10f);
                                var pdfCell = new iTextPCell(
                                    new iTextPhrase(
                                        cell.InnerText,
                                        cellFont))
                                {
                                    Padding = 5f
                                };
                                pdfTable.AddCell(pdfCell);
                            }
                        }

                        doc.Add(pdfTable);
                    }
                }

                doc.Close();
                return outputStream.ToArray();

            }, cancellationToken);
        }
    }
}
