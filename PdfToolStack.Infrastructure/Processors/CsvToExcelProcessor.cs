using ClosedXML.Excel;

namespace PdfToolStack.Infrastructure.Processors.Excel;

public sealed class CsvToExcelProcessor
{
    public Task<byte[]> ProcessAsync(byte[] csvBytes, CancellationToken cancellationToken)
        => Task.Run(() => Convert(csvBytes), cancellationToken);

    private static byte[] Convert(byte[] csvBytes)
    {
        using var reader = new StreamReader(new MemoryStream(csvBytes));
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");

        int row = 1;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var columns = ParseCsvLine(line);
            for (int col = 0; col < columns.Length; col++)
                sheet.Cell(row, col + 1).Value = columns[col];
            row++;
        }

        // Style the header row
        if (row > 1)
        {
            var headerRow = sheet.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F5E9");
        }

        // Auto-fit columns
        sheet.Columns().AdjustToContents();

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        var result = output.ToArray();

        if (result.Length == 0)
            throw new InvalidOperationException("CSV to Excel conversion produced an empty file.");

        return result;
    }

    /// <summary>
    /// Parses a single CSV line respecting quoted fields.
    /// </summary>
    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
