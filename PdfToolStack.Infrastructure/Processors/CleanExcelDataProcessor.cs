using ClosedXML.Excel;
using System.Text;

namespace PdfToolStack.Infrastructure.Processors.Excel;

public sealed record CleanDataOptions(
    bool TrimWhitespace = true,
    bool RemoveEmptyRows = true,
    bool NormalizeCase = false);

public sealed class CleanExcelDataProcessor
{
    public Task<byte[]> ProcessAsync(
        byte[] fileBytes,
        CleanDataOptions options,
        CancellationToken cancellationToken)
        => Task.Run(() => Clean(fileBytes, options), cancellationToken);

    private static byte[] Clean(byte[] fileBytes, CleanDataOptions options)
    {
        // Detect CSV vs Excel by magic bytes / content
        bool isCsv = IsCsvBytes(fileBytes);

        if (isCsv)
            return CleanCsv(fileBytes, options);

        return CleanExcel(fileBytes, options);
    }

    private static byte[] CleanExcel(byte[] fileBytes, CleanDataOptions options)
    {
        using var ms = new MemoryStream(fileBytes);
        using var workbook = new XLWorkbook(ms);

        foreach (var sheet in workbook.Worksheets)
        {
            var range = sheet.RangeUsed();
            if (range is null) continue;

            var rowsToDelete = new List<int>();

            foreach (var row in range.Rows())
            {
                bool isEmpty = true;

                foreach (var cell in row.Cells())
                {
                    var val = cell.GetString();

                    if (options.TrimWhitespace)
                        val = val.Trim();

                    if (options.NormalizeCase && !string.IsNullOrEmpty(val))
                        val = char.ToUpper(val[0]) + val[1..].ToLower();

                    cell.Value = val;

                    if (!string.IsNullOrWhiteSpace(val))
                        isEmpty = false;
                }

                if (options.RemoveEmptyRows && isEmpty)
                    rowsToDelete.Add(row.RowNumber());
            }

            // Delete empty rows in reverse order to preserve row numbers
            foreach (var rowNum in rowsToDelete.OrderByDescending(r => r))
                sheet.Row(rowNum).Delete();
        }

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        var result = output.ToArray();

        if (result.Length == 0)
            throw new InvalidOperationException("Clean data produced an empty file.");

        return result;
    }

    private static byte[] CleanCsv(byte[] fileBytes, CleanDataOptions options)
    {
        using var reader = new StreamReader(new MemoryStream(fileBytes));
        var sb = new StringBuilder();
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            var parts = line.Split(',');

            if (options.TrimWhitespace)
                parts = parts.Select(p => p.Trim()).ToArray();

            if (options.NormalizeCase)
                parts = parts.Select(p =>
                    string.IsNullOrEmpty(p) ? p :
                    char.ToUpper(p[0]) + p[1..].ToLower()).ToArray();

            if (options.RemoveEmptyRows && parts.All(string.IsNullOrWhiteSpace))
                continue;

            sb.AppendLine(string.Join(",", parts));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static bool IsCsvBytes(byte[] bytes)
    {
        // XLSX magic bytes: PK (50 4B) — ZIP format
        if (bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B)
            return false;
        // XLS magic bytes: D0 CF 11 E0
        if (bytes.Length >= 4 && bytes[0] == 0xD0 && bytes[1] == 0xCF)
            return false;
        return true;
    }
}
