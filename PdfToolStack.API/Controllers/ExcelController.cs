using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PdfToolStack.Infrastructure.Processors.Excel;

namespace PdfToolStack.API.Controllers;

[ApiController]
[Route("api/excel")]
[AllowAnonymous]
public sealed class ExcelController : ControllerBase
{
    private readonly CsvToExcelProcessor _csvToExcel;
    private readonly ExcelToCsvProcessor _excelToCsv;
    private readonly CleanExcelDataProcessor _cleanExcelData;
    private readonly RemoveDuplicatesProcessor _removeDuplicates;
    private readonly ILogger<ExcelController> _logger;

    private const long MaxFileSizeBytes = 52_428_800; // 50MB

    public ExcelController(
        CsvToExcelProcessor csvToExcel,
        ExcelToCsvProcessor excelToCsv,
        CleanExcelDataProcessor cleanExcelData,
        RemoveDuplicatesProcessor removeDuplicates,
        ILogger<ExcelController> logger)
    {
        _csvToExcel = csvToExcel;
        _excelToCsv = excelToCsv;
        _cleanExcelData = cleanExcelData;
        _removeDuplicates = removeDuplicates;
        _logger = logger;
    }

    // ── POST api/excel/csv-to-excel ───────────────────────────────────────
    [HttpPost("csv-to-excel")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> CsvToExcel(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = "File exceeds the 50MB limit." });

        if (!IsCsvFile(file))
            return BadRequest(new { error = "File must be a CSV (.csv)." });

        try
        {
            using var inputStream = file.OpenReadStream();
            var inputBytes = await ReadStreamAsync(inputStream, cancellationToken);
            var result = await _csvToExcel.ProcessAsync(inputBytes, cancellationToken);

            var outputName = Path.GetFileNameWithoutExtension(file.FileName) + ".xlsx";
            return File(result,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                outputName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CsvToExcel failed for file {FileName}", file.FileName);
            return StatusCode(500, new { error = "Failed to convert CSV to Excel. Please check the file and try again." });
        }
    }

    // ── POST api/excel/excel-to-csv ───────────────────────────────────────
    [HttpPost("excel-to-csv")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> ExcelToCsv(
        IFormFile? file,
        [FromQuery] string? sheetName,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = "File exceeds the 50MB limit." });

        if (!IsExcelFile(file))
            return BadRequest(new { error = "File must be an Excel file (.xlsx or .xls)." });

        try
        {
            using var inputStream = file.OpenReadStream();
            var inputBytes = await ReadStreamAsync(inputStream, cancellationToken);
            var result = await _excelToCsv.ProcessAsync(inputBytes, sheetName, cancellationToken);

            var outputName = Path.GetFileNameWithoutExtension(file.FileName) + ".csv";
            return File(result, "text/csv", outputName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExcelToCsv failed for file {FileName}", file.FileName);
            return StatusCode(500, new { error = "Failed to convert Excel to CSV. Please check the file and try again." });
        }
    }

    // ── POST api/excel/clean-data ─────────────────────────────────────────
    [HttpPost("clean-data")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> CleanData(
        IFormFile? file,
        [FromQuery] bool trimWhitespace = true,
        [FromQuery] bool removeEmptyRows = true,
        [FromQuery] bool normalizeCase = false,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = "File exceeds the 50MB limit." });

        if (!IsExcelOrCsvFile(file))
            return BadRequest(new { error = "File must be an Excel or CSV file." });

        try
        {
            using var inputStream = file.OpenReadStream();
            var inputBytes = await ReadStreamAsync(inputStream, cancellationToken);

            var options = new CleanDataOptions(trimWhitespace, removeEmptyRows, normalizeCase);
            var result = await _cleanExcelData.ProcessAsync(inputBytes, options, cancellationToken);

            var ext = IsCsvFile(file) ? ".csv" : ".xlsx";
            var mime = IsCsvFile(file) ? "text/csv"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            var outputName = Path.GetFileNameWithoutExtension(file.FileName) + "_cleaned" + ext;
            return File(result, mime, outputName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CleanData failed for file {FileName}", file.FileName);
            return StatusCode(500, new { error = "Failed to clean the file. Please check the file and try again." });
        }
    }

    // ── POST api/excel/remove-duplicates ──────────────────────────────────
    [HttpPost("remove-duplicates")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> RemoveDuplicates(
        IFormFile? file,
        [FromQuery] bool caseSensitive = false,
        [FromQuery] bool keepFirst = true,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = "File exceeds the 50MB limit." });

        if (!IsExcelOrCsvFile(file))
            return BadRequest(new { error = "File must be an Excel or CSV file." });

        try
        {
            using var inputStream = file.OpenReadStream();
            var inputBytes = await ReadStreamAsync(inputStream, cancellationToken);

            var options = new RemoveDuplicatesOptions(caseSensitive, keepFirst);
            var (result, removedCount) = await _removeDuplicates.ProcessAsync(inputBytes, options, cancellationToken);

            var ext = IsCsvFile(file) ? ".csv" : ".xlsx";
            var mime = IsCsvFile(file) ? "text/csv"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            var outputName = Path.GetFileNameWithoutExtension(file.FileName) + "_deduped" + ext;

            Response.Headers.Append("X-Rows-Removed", removedCount.ToString());
            return File(result, mime, outputName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RemoveDuplicates failed for file {FileName}", file.FileName);
            return StatusCode(500, new { error = "Failed to remove duplicates. Please check the file and try again." });
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static bool IsCsvFile(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return ext == ".csv";
    }

    private static bool IsExcelFile(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return ext is ".xlsx" or ".xls";
    }

    private static bool IsExcelOrCsvFile(IFormFile file)
        => IsCsvFile(file) || IsExcelFile(file);

    private static async Task<byte[]> ReadStreamAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
