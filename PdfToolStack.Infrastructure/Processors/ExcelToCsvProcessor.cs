using ClosedXML.Excel;
using System.Text;

namespace PdfToolStack.Infrastructure.Processors.Excel;

public sealed class ExcelToCsvProcessor
{
    public Task<byte[]> ProcessAsync(
        byte[] excelBytes,
        string? sheetName,
        CancellationToken cancellationToken)
        => Task.Run(() => Convert(excelBytes, sheetName), cancellationToken);

    private static byte[] Convert(byte[] excelBytes, string? sheetName)
    {
        using var ms = new MemoryStream(excelBytes);
        using var workbook = new XLWorkbook(ms);

        // Pick the requested sheet or fall back to the first one
        IXLWorksheet sheet;
        if (!string.IsNullOrWhiteSpace(sheetName) &&
            workbook.Worksheets.TryGetWorksheet(sheetName, out var named))
        {
            sheet = named;
        }
        else
        {
            sheet = workbook.Worksheets.First();
        }

        var sb = new StringBuilder();
        var range = sheet.RangeUsed();

        if (range is null)
            throw new InvalidOperationException("The selected sheet contains no data.");

        foreach (var row in range.Rows())
        {
            var cells = row.Cells().Select(c => EscapeCsvField(c.GetString()));
            sb.AppendLine(string.Join(",", cells));
        }

        var result = Encoding.UTF8.GetBytes(sb.ToString());

        if (result.Length == 0)
            throw new InvalidOperationException("Excel to CSV conversion produced an empty file.");

        return result;
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }
}
