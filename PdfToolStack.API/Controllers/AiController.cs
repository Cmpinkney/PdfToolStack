using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PdfToolStack.Application.Interfaces;
using PdfToolStack.Infrastructure.Services;
using System.Collections.Concurrent;
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

        private static readonly ConcurrentDictionary<string,
            (int Count, DateTime WindowStart)> _supportCounts = new();

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

        // POST api/ai/translate
        [HttpPost("translate")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> Translate(
            IFormFile file,
            [FromForm] string targetLanguage = "es",
            [FromForm] string languageName = "Spanish",
            CancellationToken cancellationToken = default)
        {
            if (!IsValidPdf(file))
                return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

            var userId = User.FindFirst("sub")?.Value ?? "anonymous";
            (bool allowed, IActionResult? limitResponse) = await CheckAiUsageAsync(
                userId, "translate", HaikuModel);
            if (!allowed) return limitResponse!;

            _logger.LogInformation(
                "Translate request. Language: {Lang}, File: {File}",
                languageName, file.FileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            var result = await _aiService.TranslateAsync(
                ms.ToArray(), targetLanguage, languageName, cancellationToken);

            if (!result.IsSuccess)
                return UnprocessableEntity(new { error = result.ErrorMessage });

            return Ok(new
            {
                translatedText = result.TranslatedText,
                targetLanguage = result.TargetLanguage,
                languageName = result.LanguageName
            });
        }

        // POST api/ai/rewrite
        [HttpPost("rewrite")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> Rewrite(
            IFormFile file,
            [FromForm] string instruction,
            [FromForm] string tone = "default",
            CancellationToken cancellationToken = default)
        {
            if (!IsValidPdf(file))
                return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

            if (string.IsNullOrWhiteSpace(instruction))
                return BadRequest(new { error = "Please provide a rewrite instruction." });

            var userId = User.FindFirst("sub")?.Value ?? "anonymous";
            (bool allowed, IActionResult? limitResponse) = await CheckAiUsageAsync(
                userId, "rewrite", HaikuModel);
            if (!allowed) return limitResponse!;

            _logger.LogInformation(
                "Rewrite request. Tone: {Tone}, File: {File}",
                tone, file.FileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            var result = await _aiService.RewriteAsync(
                ms.ToArray(), instruction, tone, cancellationToken);

            if (!result.IsSuccess)
                return UnprocessableEntity(new { error = result.ErrorMessage });

            return Ok(new { rewrittenText = result.RewrittenText });
        }

        // POST api/ai/rewrite/download
        [HttpPost("rewrite/download")]
        public async Task<IActionResult> DownloadRewrite(
            [FromForm] string text,
            [FromForm] string format = "txt",
            [FromForm] string fileName = "rewritten",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                return BadRequest(new { error = "No text provided." });

            try
            {
                switch (format.ToLower())
                {
                    case "docx":
                        var docxBytes = BuildDocx(text, fileName);
                        return File(docxBytes,
                            "application/vnd.openxmlformats-officedocument" +
                            ".wordprocessingml.document",
                            $"{fileName}.docx");

                    case "pdf":
                        var pdfBytes = BuildPdf(text, fileName);
                        return File(pdfBytes, "application/pdf", $"{fileName}.pdf");

                    default:
                        var txtBytes = System.Text.Encoding.UTF8.GetBytes(text);
                        return File(txtBytes, "text/plain", $"{fileName}.txt");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Download failed for format {Format}", format);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET api/ai/usage (current user)
        [HttpGet("usage")]
        [Authorize]
        public async Task<IActionResult> GetMyUsage()
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (_subscriptionService != null)
            {
                var status = await _subscriptionService.GetStatusAsync(userId);
                var (used, limit) = await _usageService.GetUsageAsync(userId, status.PlanType);
                return Ok(new { used, limit });
            }

            return Ok(new { used = 0, limit = 5 });
        }

        private static byte[] BuildDocx(string text, string title)
        {
            using var ms = new MemoryStream();

            using var wordDoc = DocumentFormat.OpenXml.Packaging
                .WordprocessingDocument.Create(ms,
                DocumentFormat.OpenXml.WordprocessingDocumentType.Document);

            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(
                new DocumentFormat.OpenXml.Wordprocessing.Body());

            var paragraphs = text.Split('\n',
                StringSplitOptions.None);

            foreach (var para in paragraphs)
            {
                var p = body.AppendChild(
                    new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
                var r = p.AppendChild(
                    new DocumentFormat.OpenXml.Wordprocessing.Run());
                r.AppendChild(
                    new DocumentFormat.OpenXml.Wordprocessing.Text(para)
                    {
                        Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve
                    });
            }

            mainPart.Document.Save();
            wordDoc.Dispose();

            return ms.ToArray();
        }

        private static byte[] BuildPdf(string text, string title)
        {
            using var ms = new MemoryStream();

            var document = new iTextSharp.text.Document(
                iTextSharp.text.PageSize.A4, 60, 60, 60, 60);

            var writer = iTextSharp.text.pdf.PdfWriter
                .GetInstance(document, ms);
            writer.CloseStream = false;

            document.Open();

            var font = iTextSharp.text.FontFactory.GetFont(
                iTextSharp.text.FontFactory.HELVETICA, 11,
                iTextSharp.text.BaseColor.Black);

            var paragraphs = text.Split('\n',
                StringSplitOptions.None);

            foreach (var para in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(para))
                {
                    document.Add(new iTextSharp.text.Paragraph(" ", font));
                }
                else
                {
                    document.Add(new iTextSharp.text.Paragraph(para, font)
                    {
                        SpacingAfter = 4f
                    });
                }
            }

            document.Close();
            ms.Position = 0;
            return ms.ToArray();
        }

        // GET api/ai/usage/{userId}
        [HttpGet("usage/{userId}")]
        public async Task<IActionResult> GetUsage(string userId)
        {
            if (_subscriptionService == null)
                return StatusCode(503, new { error = "Service unavailable." });

            var status = await _subscriptionService.GetStatusAsync(userId);
            if (status?.PlanType == null)
                return Ok(new { used = 0, limit = 5, remaining = 5, percentage = 0 });

            var (used, limit) = await _usageService.GetUsageAsync(
                userId, status.PlanType);
            return Ok(new
            {
                used,
                limit,
                remaining = limit - used,
                percentage = limit > 0 ? (double)used / limit * 100 : 0
            });
        }

        // POST api/ai/support
        [HttpPost("support")]
        [AllowAnonymous]
        public async Task<IActionResult> Support(
            [FromBody] SupportRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { error = "Message is required." });

            // Anonymous callers: 5 requests per hour per IP
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var key = $"support:ip:{ip}";
                var now = DateTime.UtcNow;
                _supportCounts.AddOrUpdate(key, (1, now), (_, existing) =>
                {
                    if ((now - existing.WindowStart).TotalHours >= 1)
                        return (1, now);
                    return (existing.Count + 1, existing.WindowStart);
                });
                if (_supportCounts[key].Count > 5)
                    return StatusCode(429, new { error = "Too many requests. Please try again later." });
            }

            var answer = await _aiService.SupportChatAsync(
                request.Message,
                request.History,
                cancellationToken);

            return Ok(new { answer });
        }

        public record SupportRequest(
            string Message,
            List<SupportMessage>? History);

        

        // ── Helpers ───────────────────────────────────────────
        private async Task<(bool Allowed, IActionResult? Response)>
            CheckAiUsageAsync(string userId, string feature, string model)
        {
            try
            {
                var planType = "free"; // default to free limits
                if (_subscriptionService != null && userId != "anonymous")
                {
                    var status = await _subscriptionService.GetStatusAsync(userId);
                    planType = status.IsActive ? status.PlanType : "free";
                }

                (bool allowed, int used, int limit) = await _usageService
                    .CheckAndLogAsync(userId, feature, model, planType);

                if (!allowed)
                    return (false, StatusCode(429, new
                    {
                        error = $"You've used your {limit} free AI requests this month. " +
                                "Upgrade to Pro for 200 requests/month.",
                        used,
                        limit,
                        upgradePath = "/pricing"
                    }));

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Usage check failed — allowing request");
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