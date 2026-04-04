using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using PdfToolStack.Application.Interfaces;
using PdfToolStack.Infrastructure.Services;
using System.Text.Json;

namespace PdfToolStack.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AiController : ControllerBase
    {
        private readonly AiService _aiService;
        private readonly IAiUsageService _usageService;
        private readonly SubscriptionService? _subscriptionService;
        private readonly ILogger<AiController> _logger;

        private const string HaikuModel = "claude-haiku-4-5-20251001";
        private const string OpusModel = "claude-opus-4-6";

        public AiController(
        AiService aiService,
        IAiUsageService usageService,
        ILogger<AiController> logger,
        SubscriptionService? subscriptionService = null)
        {
            _aiService = aiService;
            _usageService = usageService;
            _subscriptionService = subscriptionService;
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

            var userId = User.FindFirst("sub")?.Value ?? "anonymous";
            (bool allowed, IActionResult? limitResponse) = await CheckAiUsageAsync(
                userId, "extract", HaikuModel);
            if (!allowed) return limitResponse!;

            _logger.LogInformation(
                "AI extract request. Type: {Type}, File: {File}",
                type, file.FileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            var result = await _aiService.ExtractDataAsync(
                ms.ToArray(), type, cancellationToken);

            if (!result.IsSuccess)
                return UnprocessableEntity(new { error = result.ErrorMessage });

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

            var userId = User.FindFirst("sub")?.Value ?? "anonymous";
            (bool allowed, IActionResult? limitResponse) = await CheckAiUsageAsync(
                userId, "extract-excel", HaikuModel);
            if (!allowed) return limitResponse!;

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            var result = await _aiService.ExtractDataAsync(
                ms.ToArray(), type, cancellationToken);

            if (!result.IsSuccess)
                return UnprocessableEntity(new { error = result.ErrorMessage });

            try
            {
                var excelBytes = BuildExcel(result.JsonData, type);
                var outName = $"extracted_{type}_{DateTime.UtcNow:yyyyMMdd}.xlsx";
                return File(excelBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
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

            var userId = User.FindFirst("sub")?.Value ?? "anonymous";
            (bool allowed, IActionResult? limitResponse) = await CheckAiUsageAsync(
                userId, "summarize", HaikuModel);
            if (!allowed) return limitResponse!;

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

            var userId = User.FindFirst("sub")?.Value ?? "anonymous";
            (bool allowed, IActionResult? limitResponse) = await CheckAiUsageAsync(
                userId, "chat", HaikuModel);
            if (!allowed) return limitResponse!;

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            var answer = await _aiService.ChatAsync(
                ms.ToArray(), question, cancellationToken);

            return Ok(new { answer });
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

            var userId = User.FindFirst("sub")?.Value ?? "anonymous";
            (bool allowed, IActionResult? limitResponse) = await CheckAiUsageAsync(
                userId, "questions", HaikuModel);
            if (!allowed) return limitResponse!;

            count = Math.Clamp(count, 3, 15);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            var prompt =
                $"Generate exactly {count} thought-provoking questions based " +
                $"on this document. Return ONLY a JSON array of strings — " +
                $"no numbering, no markdown, no explanation. " +
                $"Example: [\"Question one?\", \"Question two?\"]";

            var rawAnswer = await _aiService.ChatAsync(
                ms.ToArray(), prompt, cancellationToken);

            try
            {
                var clean = rawAnswer
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                var questions = JsonSerializer
                    .Deserialize<List<string>>(clean)
                    ?? new List<string>();

                return Ok(new { questions });
            }
            catch
            {
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

        // POST api/ai/review
        [HttpPost("review")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> ReviewContract(
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            if (!IsValidPdf(file))
                return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

            var userId = User.FindFirst("sub")?.Value ?? "anonymous";
            (bool allowed, IActionResult? limitResponse) = await CheckAiUsageAsync(
                userId, "contract-review", OpusModel);
            if (!allowed) return limitResponse!;

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

        // POST api/ai/ocr
        [HttpPost("ocr")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> OcrPdf(
            IFormFile file,
            [FromQuery] string language = "eng",
            CancellationToken cancellationToken = default)
        {
            if (!IsValidPdf(file))
                return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

            var userId = User.FindFirst("sub")?.Value ?? "anonymous";
            (bool allowed, IActionResult? limitResponse) = await CheckAiUsageAsync(
                userId, "ocr", HaikuModel);
            if (!allowed) return limitResponse!;

            _logger.LogInformation(
                "OCR request. Language: {Lang}, File: {File}",
                language, file.FileName);

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, cancellationToken);

                var tessDataPath = Path.Combine(
                    AppContext.BaseDirectory, "tessdata");

                var processor = new PdfToolStack.Infrastructure
                    .Processors.PdfOcrProcessor(tessDataPath);

                var outputBytes = await processor.ProcessAsync(
                    ms.ToArray(), language, cancellationToken);

                return File(outputBytes, "application/pdf",
                    Path.GetFileNameWithoutExtension(file.FileName)
                    + "_searchable.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR failed");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET api/ai/usage/{userId}
        [HttpGet("usage/{userId}")]
        public async Task<IActionResult> GetUsage(string userId)
        {
            var status = await _subscriptionService.GetStatusAsync(userId);
            var (used, limit) = await _usageService.GetUsageAsync(
                userId, status.PlanType);
            return Ok(new
            {
                used,
                limit,
                remaining = limit - used,
                percentage = (double)used / limit * 100
            });
        }

        // ── Helpers ───────────────────────────────────────────
        private async Task<(bool Allowed, IActionResult? Response)>
        CheckAiUsageAsync(string userId, string feature, string model)
        {
            try
            {
                var planType = "monthly"; // default to Pro limits
                if (_subscriptionService != null)
                {
                    var status = await _subscriptionService
                        .GetStatusAsync(userId);
                    planType = status.PlanType;
                }

                (bool allowed, int used, int limit) = await _usageService
                    .CheckAndLogAsync(userId, feature, model, planType);

                if (!allowed)
                    return (false, StatusCode(429, new
                    {
                        error = $"Monthly AI limit reached ({limit} requests). " +
                                "Upgrade your plan for more.",
                        used,
                        limit
                    }));

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Usage check failed — allowing request");
                return (true, null);
            }
        }

        private static bool IsValidPdf(IFormFile? file)
        {
            if (file == null || file.Length == 0) return false;
            if (file.Length > 52_428_800) return false;

            using var stream = file.OpenReadStream();
            var header = new byte[4];
            stream.Read(header, 0, 4);
            return header[0] == 0x25 && header[1] == 0x50 &&
                   header[2] == 0x44 && header[3] == 0x46;
        }

        private static byte[] BuildExcel(string json, string type)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Extracted Data");

            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            int row = 1;

            void StyleHeader(IXLCell cell)
            {
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
                cell.Style.Font.FontColor = XLColor.White;
            }

            if (type == "invoice")
            {
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

                if (root.TryGetProperty("lineItems", out var items) &&
                    items.ValueKind == JsonValueKind.Array &&
                    items.GetArrayLength() > 0)
                {
                    ws.Cell(row, 1).Value = "Line Items";
                    ws.Cell(row, 1).Style.Font.Bold = true;
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

        private static string GetStr(JsonElement el, string key) =>
            el.TryGetProperty(key, out var v) &&
            v.ValueKind != JsonValueKind.Null
                ? v.ToString() : string.Empty;

        private static string ToLabel(string camelCase) =>
            System.Text.RegularExpressions.Regex
                .Replace(camelCase, "([A-Z])", " $1")
                .Trim()
                .Replace("_", " ");
    }
}