using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using iTextSharp.text.html.simpleparser;
using Microsoft.AspNetCore.Mvc;
using PdfToolStack.Infrastructure.Services;
using System.Text;
using System.Text.Json;

namespace PdfToolStack.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AiController : ControllerBase
    {
        private readonly AiService _aiService;
        private readonly ILogger<AiController> _logger;

        public AiController(
            AiService aiService,
            ILogger<AiController> logger)
        {
            _aiService = aiService;
            _logger = logger;
        }

        // POST api/ai/extract
        [HttpPost("extract")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> Extract(
            IFormFile file,
            [FromQuery] string type = "invoice",
            CancellationToken cancellationToken = default)
        {
            if (!IsValidPdf(file))
                return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

            _logger.LogInformation(
                "AI extract request. Type: {Type}, File: {File}",
                type, file.FileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            var result = await _aiService.ExtractDataAsync(
                ms.ToArray(), type, cancellationToken);

            if (!result.IsSuccess)
                return UnprocessableEntity(
                    new { error = result.ErrorMessage });

            return Ok(new
            {
                json = result.JsonData,
                extractionType = result.ExtractionType
            });
        }

        // POST api/ai/extract/excel
        [HttpPost("extract/excel")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> ExtractToExcel(
            IFormFile file,
            [FromQuery] string type = "invoice",
            CancellationToken cancellationToken = default)
        {
            if (!IsValidPdf(file))
                return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            var result = await _aiService.ExtractDataAsync(
                ms.ToArray(), type, cancellationToken);

            if (!result.IsSuccess)
                return UnprocessableEntity(
                    new { error = result.ErrorMessage });

            try
            {
                var excelBytes = BuildExcel(result.JsonData, type);
                var outName = $"extracted_{type}_{DateTime.UtcNow:yyyyMMdd}.xlsx";
                return File(excelBytes,
                    "application/vnd.openxmlformats-officedocument" +
                    ".spreadsheetml.sheet",
                    outName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excel build failed");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST api/ai/summarize
        [HttpPost("summarize")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> Summarize(
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            if (!IsValidPdf(file))
                return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            var summary = await _aiService.SummarizeAsync(
                ms.ToArray(), cancellationToken);

            return Ok(new { summary });
        }

        // POST api/ai/chat
        [HttpPost("chat")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> Chat(
            IFormFile file,
            [FromForm] string question,
            CancellationToken cancellationToken = default)
        {
            if (!IsValidPdf(file))
                return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

            if (string.IsNullOrWhiteSpace(question))
                return BadRequest(new { error = "No question provided." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            var answer = await _aiService.ChatAsync(
                ms.ToArray(), question, cancellationToken);

            return Ok(new { answer });
        }

        // POST api/ai/review
        [HttpPost("review")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> ReviewContract(
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            if (!IsValidPdf(file))
                return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

            _logger.LogInformation(
                "Contract review request. File: {File}", file.FileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            var result = await _aiService.ReviewContractAsync(
                ms.ToArray(), cancellationToken);

            if (!result.IsSuccess)
                return UnprocessableEntity(new { error = result.ErrorMessage });

            return Ok(new { json = result.JsonData });
        }

        private static byte[] BuildExcel(string json, string type)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Extracted Data");

            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            int row = 1;

            // Header styling helper
            void StyleHeader(IXLCell cell)
            {
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#2563EB");
                cell.Style.Font.FontColor = XLColor.White;
            }

            if (type == "invoice")
            {
                // Summary section
                ws.Cell(row, 1).Value = "Field";
                ws.Cell(row, 2).Value = "Value";
                StyleHeader(ws.Cell(row, 1));
                StyleHeader(ws.Cell(row, 2));
                row++;

                var summaryFields = new[]
                {
                    "vendor", "invoiceNumber", "invoiceDate",
                    "dueDate", "totalAmount", "taxAmount",
                    "currency", "billTo", "notes"
                };

                foreach (var field in summaryFields)
                {
                    if (root.TryGetProperty(field, out var val) &&
                        val.ValueKind != JsonValueKind.Null)
                    {
                        ws.Cell(row, 1).Value = ToLabel(field);
                        ws.Cell(row, 2).Value = val.ToString();
                        row++;
                    }
                }

                row += 2;

                // Line items section
                if (root.TryGetProperty("lineItems", out var items) &&
                    items.ValueKind == JsonValueKind.Array &&
                    items.GetArrayLength() > 0)
                {
                    ws.Cell(row, 1).Value = "Line Items";
                    ws.Cell(row, 1).Style.Font.Bold = true;
                    ws.Cell(row, 1).Style.Font.FontSize = 12;
                    row++;

                    ws.Cell(row, 1).Value = "Description";
                    ws.Cell(row, 2).Value = "Quantity";
                    ws.Cell(row, 3).Value = "Unit Price";
                    ws.Cell(row, 4).Value = "Total";
                    StyleHeader(ws.Cell(row, 1));
                    StyleHeader(ws.Cell(row, 2));
                    StyleHeader(ws.Cell(row, 3));
                    StyleHeader(ws.Cell(row, 4));
                    row++;

                    foreach (var item in items.EnumerateArray())
                    {
                        ws.Cell(row, 1).Value = GetStr(item, "description");
                        ws.Cell(row, 2).Value = GetStr(item, "quantity");
                        ws.Cell(row, 3).Value = GetStr(item, "unitPrice");
                        ws.Cell(row, 4).Value = GetStr(item, "total");
                        row++;
                    }
                }
            }
            else
            {
                // Generic flat extraction
                ws.Cell(row, 1).Value = "Field";
                ws.Cell(row, 2).Value = "Value";
                StyleHeader(ws.Cell(row, 1));
                StyleHeader(ws.Cell(row, 2));
                row++;

                FlattenJson(root, ws, ref row, string.Empty);
            }

            ws.Columns().AdjustToContents();
            ws.Column(1).Width = Math.Max(ws.Column(1).Width, 20);
            ws.Column(2).Width = Math.Max(ws.Column(2).Width, 30);

            using var outMs = new MemoryStream();
            wb.SaveAs(outMs);
            return outMs.ToArray();
        }

        private static void FlattenJson(
            JsonElement element,
            IXLWorksheet ws,
            ref int row,
            string prefix)
        {
            foreach (var prop in element.EnumerateObject())
            {
                var label = string.IsNullOrEmpty(prefix)
                    ? ToLabel(prop.Name)
                    : $"{prefix} › {ToLabel(prop.Name)}";

                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    FlattenJson(prop.Value, ws, ref row, label);
                }
                else if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    var i = 1;
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        ws.Cell(row, 1).Value = $"{label} [{i}]";
                        ws.Cell(row, 2).Value = item.ToString();
                        row++;
                        i++;
                    }
                }
                else if (prop.Value.ValueKind != JsonValueKind.Null)
                {
                    ws.Cell(row, 1).Value = label;
                    ws.Cell(row, 2).Value = prop.Value.ToString();
                    row++;
                }
            }
        }

        // POST api/ai/questions
        [HttpPost("questions")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> GenerateQuestions(
            IFormFile file,
            [FromForm] int count = 5,
            CancellationToken cancellationToken = default)
        {
            if (!IsValidPdf(file))
                return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

            count = Math.Clamp(count, 3, 15);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            // Reuse AiService chat with a questions-specific prompt
            var prompt =
                $"Generate exactly {count} thought-provoking questions based " +
                $"on this document. Return ONLY a JSON array of strings — " +
                $"no numbering, no markdown, no explanation. " +
                $"Example: [\"Question one?\", \"Question two?\"]";

            var rawAnswer = await _aiService.ChatAsync(
                ms.ToArray(), prompt, cancellationToken);

            try
            {
                // Strip markdown fences if present
                var clean = rawAnswer
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                var questions = System.Text.Json.JsonSerializer
                    .Deserialize<List<string>>(clean)
                    ?? new List<string>();

                return Ok(new { questions });
            }
            catch
            {
                // Fallback: split by newlines if JSON parse fails
                var lines = rawAnswer
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.TrimStart('-', '*', '0', '1', '2', '3',
                        '4', '5', '6', '7', '8', '9', '.', ' '))
                    .Where(l => l.Length > 10)
                    .Take(count)
                    .ToList();

                return Ok(new { questions = lines });
            }
        }

        private static string GetStr(JsonElement el, string key) =>
            el.TryGetProperty(key, out var v) &&
            v.ValueKind != JsonValueKind.Null
                ? v.ToString() : string.Empty;

        private static string ToLabel(string camelCase) =>
            System.Text.RegularExpressions.Regex
                .Replace(camelCase, "([A-Z])", " $1")
                .Trim()
                .Replace("_", " ");

        private static bool IsValidPdf(IFormFile file)
        {
            if (file == null || file.Length == 0) return false;
            if (file.Length > 52_428_800) return false; // 50MB max

            // Check PDF magic bytes
            using var stream = file.OpenReadStream();
            var header = new byte[4];
            stream.Read(header, 0, 4);
            return header[0] == 0x25 && header[1] == 0x50 &&
                   header[2] == 0x44 && header[3] == 0x46;
        }
    }
}