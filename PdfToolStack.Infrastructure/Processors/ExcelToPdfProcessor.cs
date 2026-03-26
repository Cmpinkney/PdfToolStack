using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Domain.Enums;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using iTextDocument = iTextSharp.text.Document;
using iTextPageSize = iTextSharp.text.PageSize;
using iTextParagraph = iTextSharp.text.Paragraph;
using iTextFont = iTextSharp.text.Font;
using iTextBaseFont = iTextSharp.text.pdf.BaseFont;
using iTextPTable = iTextSharp.text.pdf.PdfPTable;
using iTextPCell = iTextSharp.text.pdf.PdfPCell;
using iTextPhrase = iTextSharp.text.Phrase;
using iTextBaseColor = iTextSharp.text.BaseColor;
using iTextPdfWriter = iTextSharp.text.pdf.PdfWriter;
using OpenXmlRow = DocumentFormat.OpenXml.Spreadsheet.Row;
using OpenXmlCell = DocumentFormat.OpenXml.Spreadsheet.Cell;

namespace PdfToolStack.Infrastructure.Processors
{
    public class ExcelToPdfProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.ExcelToPdf;

        public async Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                using var excelStream =
                    new MemoryStream(inputBytes);
                using var workbook =
                    SpreadsheetDocument.Open(
                        excelStream, false);

                var workbookPart = workbook.WorkbookPart;
                if (workbookPart == null)
                    throw new Exception(
                        "Could not read Excel file.");

                using var outputStream = new MemoryStream();
                using var doc = new iTextDocument(
                    iTextPageSize.A4.Rotate(),
                    20f, 20f, 30f, 20f);

                iTextPdfWriter.GetInstance(
                    doc, outputStream);
                doc.Open();

                var baseFont = iTextBaseFont.CreateFont(
                    iTextBaseFont.HELVETICA,
                    iTextBaseFont.CP1252,
                    iTextBaseFont.NOT_EMBEDDED);

                var sheets = workbookPart.Workbook
                    .Descendants<Sheet>().ToList();
                bool firstSheet = true;

                foreach (var sheet in sheets)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    if (!firstSheet) doc.NewPage();
                    firstSheet = false;

                    var sheetName =
                        sheet.Name?.Value ?? "Sheet";
                    doc.Add(new iTextParagraph(sheetName,
                        new iTextFont(baseFont, 14f,
                            iTextFont.BOLD))
                    { SpacingAfter = 8f });

                    var worksheetPart =
                        workbookPart.GetPartById(sheet.Id!)
                        as WorksheetPart;
                    if (worksheetPart == null) continue;

                    var rows = worksheetPart.Worksheet
                        .Descendants<OpenXmlRow>().ToList();
                    if (!rows.Any()) continue;

                    int colCount = rows
                        .Max(r => r
                            .Elements<OpenXmlCell>()
                            .Count());
                    if (colCount == 0) continue;

                    var table = new iTextPTable(colCount)
                    {
                        WidthPercentage = 100f,
                        SpacingAfter = 10f
                    };

                    bool isHeader = true;
                    foreach (var row in rows)
                    {
                        cancellationToken
                            .ThrowIfCancellationRequested();

                        var cells = row
                            .Elements<OpenXmlCell>()
                            .ToList();

                        for (int i = 0;
                            i < colCount; i++)
                        {
                            var cellValue =
                                i < cells.Count
                                ? GetCellValue(
                                    workbookPart,
                                    cells[i])
                                : "";

                            iTextPCell pdfCell;
                            if (isHeader)
                            {
                                var headerFont =
                                    new iTextFont(
                                        baseFont, 9f,
                                        iTextFont.BOLD,
                                        iTextBaseColor.White);
                                pdfCell = new iTextPCell(
                                    new iTextPhrase(
                                        cellValue,
                                        headerFont))
                                {
                                    Padding = 4f,
                                    BackgroundColor =
                                        new iTextBaseColor(
                                            37, 99, 235)
                                };
                            }
                            else
                            {
                                var bodyFont =
                                    new iTextFont(
                                        baseFont, 9f);
                                pdfCell = new iTextPCell(
                                    new iTextPhrase(
                                        cellValue,
                                        bodyFont))
                                {
                                    Padding = 4f
                                };
                            }

                            table.AddCell(pdfCell);
                        }
                        isHeader = false;
                    }

                    doc.Add(table);
                }

                doc.Close();
                return outputStream.ToArray();

            }, cancellationToken);
        }

        private static string GetCellValue(
            WorkbookPart workbookPart,
            OpenXmlCell cell)
        {
            var value = cell.CellValue?.Text ?? "";
            if (cell.DataType?.Value ==
                CellValues.SharedString)
            {
                var sstPart =
                    workbookPart.SharedStringTablePart;
                if (sstPart != null &&
                    int.TryParse(value, out int idx))
                {
                    value = sstPart.SharedStringTable
                        .Elements<SharedStringItem>()
                        .ElementAtOrDefault(idx)
                        ?.InnerText ?? value;
                }
            }
            return value;
        }
    }
}