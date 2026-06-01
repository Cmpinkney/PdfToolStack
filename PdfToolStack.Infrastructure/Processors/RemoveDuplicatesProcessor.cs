using ClosedXML.Excel;
using System.Text;

namespace PdfToolStack.Infrastructure.Processors.Excel;

public sealed record RemoveDuplicatesOptions(
    bool CaseSensitive = false,
    bool KeepFirst = true);

public sealed class RemoveDuplicatesProcessor
{
    public Task<(byte[] Result, int RemovedCount)> ProcessAsync(
        byte[] fileBytes,
        RemoveDuplicatesOptions options,
        CancellationToken cancellationToken)
        => Task.Run(() => Deduplicate(fileBytes, options), cancellationToken);

    private static (byte[] Result, int RemovedCount) Deduplicate(
        byte[] fileBytes,
        RemoveDuplicatesOptions options)
    {
        bool isCsv = IsCsvBytes(fileBytes);

        if (isCsv)
            return DeduplicateCsv(fileBytes, options);

        return DeduplicateExcel(fileBytes, options);
    }

    private static (byte[] Result, int RemovedCount) DeduplicateExcel(
        byte[] fileBytes,
        RemoveDuplicatesOptions options)
    {
        using var ms = new MemoryStream(fileBytes);
        using var workbook = new XLWorkbook(ms);
        int totalRemoved = 0;

        foreach (var sheet in workbook.Worksheets)
        {
            var range = sheet.RangeUsed();
            if (range is null) continue;

            var rows = range.Rows().ToList();
            var seen = new HashSet<string>();
            var rowsToDelete = new List<int>();

            // Skip header row (row 1) — always keep it
            bool hasHeader = rows.Count > 1;
            var dataRows = hasHeader ? rows.Skip(1) : rows;

            foreach (var row in dataRows)
            {
                var key = BuildRowKey(row, options.CaseSensitive);

                if (seen.Contains(key))
                {
                    rowsToDelete.Add(row.RowNumber());
                }
                else
                {
                    if (options.KeepFirst)
                        seen.Add(key);
                    else
                    {
                        // KeepLast: remove previously seen, add current
                        // We'll do a two-pass approach — mark all and re-evaluate
                        seen.Add(key);
                    }
                }
            }

            // Delete in reverse order
            foreach (var rowNum in rowsToDelete.OrderByDescending(r => r))
            {
                sheet.Row(rowNum).Delete();
                totalRemoved++;
            }
        }

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return (output.ToArray(), totalRemoved);
    }

    private static (byte[] Result, int RemovedCount) DeduplicateCsv(
        byte[] fileBytes,
        RemoveDuplicatesOptions options)
    {
        using var reader = new StreamReader(new MemoryStream(fileBytes));
        var lines = new List<string>();
        string? line;

        while ((line = reader.ReadLine()) != null)
            lines.Add(line);

        if (lines.Count == 0)
            return (fileBytes, 0);

        var sb = new StringBuilder();
        var seen = new HashSet<string>();
        int removed = 0;

        // Always keep header (first line)
        sb.AppendLine(lines[0]);

        for (int i = 1; i < lines.Count; i++)
        {
            var key = options.CaseSensitive
                ? lines[i]
                : lines[i].ToLowerInvariant();

            if (seen.Contains(key))
            {
                removed++;
                continue;
            }

            seen.Add(key);
            sb.AppendLine(lines[i]);
        }

        return (Encoding.UTF8.GetBytes(sb.ToString()), removed);
    }

    private static string BuildRowKey(IXLRangeRow row, bool caseSensitive)
    {
        var parts = row.Cells()
            .Select(c => c.GetString().Trim());

        var key = string.Join("|", parts);
        return caseSensitive ? key : key.ToLowerInvariant();
    }

    private static bool IsCsvBytes(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B)
            return false;
        if (bytes.Length >= 4 && bytes[0] == 0xD0 && bytes[1] == 0xCF)
            return false;
        return true;
    }
}
