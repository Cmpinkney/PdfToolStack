using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PdfToolStack.Infrastructure.Processors
{
    public class PdfToExcelProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.PdfToExcel;

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
        {
            using var workbook = new XLWorkbook();

            using var pdf = PdfDocument.Open(inputBytes);
            int pageNum = 1;

            foreach (var page in pdf.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sheet = workbook.Worksheets.Add($"Page {pageNum}");

                // Group words into lines by Y position
                var words = page.GetWords().ToList();
                var lines = words
                    .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 0))
                    .OrderByDescending(g => g.Key)
                    .ToList();

                int row = 1;
                foreach (var line in lines)
                {
                    // Sort words left to right
                    var lineWords = line
                        .OrderBy(w => w.BoundingBox.Left)
                        .ToList();

                    // Try to detect columns by X gaps
                    var columns = SplitIntoColumns(lineWords);

                    for (int col = 1; col <= columns.Count; col++)
                    {
                        sheet.Cell(row, col).Value = columns[col - 1];
                    }
                    row++;
                }

                // Auto-fit columns
                sheet.Columns().AdjustToContents();
                pageNum++;
            }

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return Task.FromResult(ms.ToArray());
        }

        private static List<string> SplitIntoColumns(List<Word> words)
        {
            if (!words.Any()) return new List<string>();

            var columns = new List<string>();
            var currentGroup = new List<string> { words[0].Text };
            double lastRight = words[0].BoundingBox.Right;

            for (int i = 1; i < words.Count; i++)
            {
                var word = words[i];
                var gap = word.BoundingBox.Left - lastRight;

                // Gap > 20 units = new column
                if (gap > 20)
                {
                    columns.Add(string.Join(" ", currentGroup));
                    currentGroup = new List<string>();
                }

                currentGroup.Add(word.Text);
                lastRight = word.BoundingBox.Right;
            }

            if (currentGroup.Any())
                columns.Add(string.Join(" ", currentGroup));

            return columns;
        }
    }
}