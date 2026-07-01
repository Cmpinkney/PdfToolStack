using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfToolStack.Application.DTOs;
using PdfToolStack.Application.Interfaces;
using PdfToolStack.Infrastructure.Data;
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
        private readonly AppDbContext _db;

        private const string HaikuModel = "claude-haiku-4-5-20251001";
        private const string OpusModel = "claude-opus-4-6";
        private const string UnsupportedUnicodePdfMessage =
            "PDF translation output currently supports Latin-script languages only. Choose a Latin-script target language while Unicode PDF font support is added.";

        private static readonly HashSet<string> LatinScriptPdfLanguageCodes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "es", "fr", "de", "it", "pt", "nl", "pl", "tr",
                "sv", "da", "fi", "no", "cs", "ro", "hu", "vi",
                "id", "ms"
            };

        private static readonly HashSet<string> LatinScriptPdfLanguageNames =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "spanish", "french", "german", "italian", "portuguese",
                "dutch", "polish", "turkish", "swedish", "danish",
                "finnish", "norwegian", "czech", "romanian", "hungarian",
                "vietnamese", "indonesian", "malay"
            };

        private static readonly ConcurrentDictionary<string,
            (int Count, DateTime WindowStart)> _supportCounts = new();

        public AiController(
            AiService aiService,
            IAiUsageService usageService,
            ILogger<AiController> logger,
            AppDbContext db,
            SubscriptionService? subscriptionService = null)
        {
            _aiService = aiService;
            _usageService = usageService;
            _subscriptionService = subscriptionService;
            _logger = logger;
            _db = db;
        }

        // POST api/ai/extract
        [HttpPost("extract")]
        [Authorize]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> Extract(
            IFormFile file,
            [FromQuery] string type = "invoice",
            CancellationToken cancellationToken = default)
        {
            string? userId = null;
            int? pageCount = null;
            var fileName = file?.FileName ?? "(missing)";
            var fileSize = file?.Length ?? 0;

            try
            {
                _logger.LogInformation(
                    "Invoice extract upload received. Type: {Type}, FileName: {FileName}, FileSize: {FileSize}",
                    type,
                    fileName,
                    fileSize);

                if (!IsValidPdf(file))
                    return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

                userId = GetRequiredUserId();
                if (userId == null) return Unauthorized();

                (bool allowed, IActionResult? limitResponse) = await CheckAiUsageAsync(
                    userId, "extract", HaikuModel);
                if (!allowed) return limitResponse!;

                using var ms = new MemoryStream();
                await file!.CopyToAsync(ms, cancellationToken);
                var pdfBytes = ms.ToArray();
                pageCount = TryCountPdfPages(pdfBytes);
                var isPro = await IsProUserAsync(userId);

                _logger.LogInformation(
                    "Invoice extract upload processed. UserId: {UserId}, Type: {Type}, FileName: {FileName}, FileSize: {FileSize}, PdfBytes: {PdfBytes}, PageCount: {PageCount}, IsPro: {IsPro}",
                    userId,
                    type,
                    fileName,
                    fileSize,
                    pdfBytes.Length,
                    pageCount,
                    isPro);

                var result = await _aiService.ExtractDataAsync(
                    pdfBytes,
                    type,
                    cancellationToken,
                    userId,
                    isPro,
                    pageCount);

                if (!result.IsSuccess)
                    return ToExtractionFailureResponse(result);

                return Ok(new
                {
                    json = result.JsonData,
                    extractionType = result.ExtractionType,
                    textPreview = result.TextPreview,
                    ocrFallbackUsed = result.OcrFallbackUsed,
                    ocrWarning = result.OcrWarning
                });
            }
            catch (Exception ex)
            {
                LogExceptionDetails(
                    ex,
                    "Invoice extract failed unexpectedly. UserId: {UserId}, Type: {Type}, FileName: {FileName}, FileSize: {FileSize}, PageCount: {PageCount}",
                    userId,
                    type,
                    fileName,
                    fileSize,
                    pageCount);

                return UnprocessableEntity(new
                {
                    error = "Unable to extract structured invoice data from this document."
                });
            }
        }

        // POST api/ai/fraud-check
        [HttpPost("fraud-check")]
        [Authorize]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> AnalyzeFraud(
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            string? userId = null;
            var fileName = file?.FileName ?? "(missing)";
            var fileSize = file?.Length ?? 0;

            try
            {
                _logger.LogInformation(
                    "Fraud check upload received. FileName: {FileName}, FileSize: {FileSize}",
                    fileName, fileSize);

                if (!IsValidPdf(file))
                    return BadRequest(new { error = "A valid PDF file under 50MB is required." });

                userId = GetRequiredUserId();
                if (userId is null)
                    return Unauthorized(new { error = "Authentication required." });

                var isPro = await IsProUserAsync(userId);
                if (!isPro)
                    return StatusCode(403, new
                    {
                        error = "Invoice fraud detection is a Pro feature.",
                        upgradeUrl = "/pricing"
                    });

                (bool allowed, IActionResult? limitResponse) = await CheckAiUsageAsync(
                    userId, "fraud-check", "claude-sonnet-4-5");
                if (!allowed) return limitResponse!;

                using var ms = new MemoryStream();
                await file!.CopyToAsync(ms, cancellationToken);
                var pdfBytes = ms.ToArray();
                var pageCount = TryCountPdfPages(pdfBytes);

                var extractResult = await _aiService.ExtractDataAsync(
                    pdfBytes, "invoice", cancellationToken, userId, isPro, pageCount);

                if (!extractResult.IsSuccess || string.IsNullOrWhiteSpace(extractResult.JsonData))
                    return UnprocessableEntity(new
                    {
                        error = "Could not extract invoice data from this PDF. Please ensure it is a valid invoice."
                    });

                var vendorName = ExtractVendorName(extractResult.JsonData);
                var history = await GetVendorHistoryAsync(userId, vendorName, cancellationToken);

                var fraudResult = await _aiService.AnalyzeFraudAsync(
                    extractResult.JsonData, history, cancellationToken);

                if (fraudResult.IsServiceError)
                    return StatusCode(503, new { error = "Fraud analysis service temporarily unavailable." });

                await SaveFraudAnalysisAsync(userId, extractResult.JsonData, fraudResult, cancellationToken);

                _logger.LogInformation(
                    "[AUDIT] FraudCheck UserId={UserId} Vendor={Vendor} RiskLevel={RiskLevel} Score={Score}",
                    userId, vendorName, fraudResult.RiskLevel, fraudResult.RiskScore);

                return Ok(new
                {
                    extractedInvoice = JsonSerializer.Deserialize<object>(extractResult.JsonData),
                    fraudAnalysis = fraudResult,
                    vendor = vendorName,
                    historyCount = history.Count
                });
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex,
                    "Fraud check failed. FileName: {FileName}, FileSize: {FileSize}, UserId: {UserId}",
                    fileName, fileSize, userId ?? "unknown");
                return UnprocessableEntity(new { error = "Fraud analysis failed. Please try again." });
            }
        }

        private string ExtractVendorName(string invoiceJson)
        {
            try
            {
                var doc = JsonDocument.Parse(invoiceJson);
                if (doc.RootElement.TryGetProperty("vendor", out var vendor))
                    return vendor.GetString() ?? "Unknown Vendor";
            }
            catch { }
            return "Unknown Vendor";
        }

        private string? ExtractField(string invoiceJson, string fieldName)
        {
            try
            {
                var doc = JsonDocument.Parse(invoiceJson);
                if (doc.RootElement.TryGetProperty(fieldName, out var val))
                    return val.GetString();
            }
            catch { }
            return null;
        }

        private DateTime? ExtractDateField(string invoiceJson, string fieldName)
        {
            try
            {
                var raw = ExtractField(invoiceJson, fieldName);
                if (raw is not null && DateTime.TryParse(raw, out var date))
                    return date;
            }
            catch { }
            return null;
        }

        private decimal ExtractAmountField(string invoiceJson, string fieldName)
        {
            try
            {
                var raw = ExtractField(invoiceJson, fieldName);
                if (raw is not null)
                {
                    var cleaned = raw.Trim().TrimStart('$', '€', '£', '¥').Replace(",", "");
                    if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var amount))
                        return amount;
                }
            }
            catch { }
            return 0m;
        }

        private async Task<List<FraudAnalysisHistoryItem>> GetVendorHistoryAsync(
            string userId, string vendorName, CancellationToken cancellationToken)
        {
            try
            {
                return await _db.FraudAnalyses
                    .Where(f => f.UserId == userId && f.VendorName == vendorName)
                    .OrderByDescending(f => f.InvoiceDate)
                    .Take(20)
                    .Select(f => new FraudAnalysisHistoryItem
                    {
                        InvoiceNumber = f.InvoiceNumber,
                        Amount = f.InvoiceAmount,
                        Currency = f.Currency,
                        InvoiceDate = f.InvoiceDate,
                        Notes = f.RiskLevel
                    })
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load vendor history for {Vendor}", vendorName);
                return new List<FraudAnalysisHistoryItem>();
            }
        }

        private async Task SaveFraudAnalysisAsync(
            string userId,
            string invoiceJson,
            FraudAnalysisResult result,
            CancellationToken cancellationToken)
        {
            try
            {
                var entity = new PdfToolStack.Domain.Entities.FraudAnalysis
                {
                    UserId = userId,
                    VendorName = ExtractVendorName(invoiceJson),
                    InvoiceNumber = ExtractField(invoiceJson, "invoiceNumber") ?? string.Empty,
                    InvoiceDate = ExtractDateField(invoiceJson, "invoiceDate") ?? DateTime.UtcNow,
                    InvoiceAmount = ExtractAmountField(invoiceJson, "totalAmount"),
                    Currency = ExtractField(invoiceJson, "currency") ?? "USD",
                    RiskScore = result.RiskScore,
                    RiskLevel = result.RiskLevel,
                    Recommendation = result.Recommendation,
                    FlagsJson = JsonSerializer.Serialize(result.Flags),
                    RawInvoiceJson = invoiceJson,
                    AnalyzedAt = DateTime.UtcNow
                };

                _db.FraudAnalyses.Add(entity);
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not save fraud analysis for user {UserId}", userId);
                // Non-fatal — don't fail the request if save fails
            }
        }

        // POST api/ai/extract/excel
        [HttpPost("extract/excel")]
        [Authorize]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> ExtractToExcel(
            IFormFile file,
            [FromQuery] string type = "invoice",
            CancellationToken cancellationToken = default)
        {
            string? userId = null;
            int? pageCount = null;
            var fileName = file?.FileName ?? "(missing)";
            var fileSize = file?.Length ?? 0;

            try
            {
                _logger.LogInformation(
                    "Invoice extract Excel upload received. Type: {Type}, FileName: {FileName}, FileSize: {FileSize}",
                    type,
                    fileName,
                    fileSize);

                if (!IsValidPdf(file))
                    return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

                userId = GetRequiredUserId();
                if (userId == null) return Unauthorized();

                (bool allowed, IActionResult? limitResponse) = await CheckAiUsageAsync(
                    userId, "extract-excel", HaikuModel);
                if (!allowed) return limitResponse!;

                using var ms = new MemoryStream();
                await file!.CopyToAsync(ms, cancellationToken);
                var pdfBytes = ms.ToArray();
                pageCount = TryCountPdfPages(pdfBytes);
                var isPro = await IsProUserAsync(userId);

                _logger.LogInformation(
                    "Invoice extract Excel upload processed. UserId: {UserId}, Type: {Type}, FileName: {FileName}, FileSize: {FileSize}, PdfBytes: {PdfBytes}, PageCount: {PageCount}, IsPro: {IsPro}",
                    userId,
                    type,
                    fileName,
                    fileSize,
                    pdfBytes.Length,
                    pageCount,
                    isPro);

                var result = await _aiService.ExtractDataAsync(
                    pdfBytes,
                    type,
                    cancellationToken,
                    userId,
                    isPro,
                    pageCount);

                if (!result.IsSuccess)
                    return ToExtractionFailureResponse(result);

                _logger.LogInformation(
                    "Invoice Excel export generation starting. UserId: {UserId}, Type: {Type}, FileName: {FileName}, JsonLength: {JsonLength}",
                    userId,
                    type,
                    fileName,
                    result.JsonData.Length);

                var excelBytes = BuildExcel(result.JsonData, type);
                var outName = $"extracted_{type}_{DateTime.UtcNow:yyyyMMdd}.xlsx";

                _logger.LogInformation(
                    "Invoice Excel export generation completed. UserId: {UserId}, Type: {Type}, FileName: {FileName}, OutputBytes: {OutputBytes}, OcrFallbackUsed: {OcrFallbackUsed}",
                    userId,
                    type,
                    fileName,
                    excelBytes.Length,
                    result.OcrFallbackUsed);

                return File(excelBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    outName);
            }
            catch (Exception ex)
            {
                LogExceptionDetails(
                    ex,
                    "Invoice Excel export failed. UserId: {UserId}, Type: {Type}, FileName: {FileName}, FileSize: {FileSize}, PageCount: {PageCount}",
                    userId,
                    type,
                    fileName,
                    fileSize,
                    pageCount);

                return UnprocessableEntity(new
                {
                    error = "Unable to generate Excel output for this extraction."
                });
            }
        }

        // POST api/ai/summarize
        [HttpPost("summarize")]
        [Authorize]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> Summarize(
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            if (!IsValidPdf(file))
                return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

            var userId = GetRequiredUserId();
            if (userId == null) return Unauthorized();

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
        [Authorize]
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

            var userId = GetRequiredUserId();
            if (userId == null) return Unauthorized();

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
        [Authorize]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> GenerateQuestions(
            IFormFile file,
            [FromForm] int count = 5,
            CancellationToken cancellationToken = default)
        {
            if (!IsValidPdf(file))
                return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

            var userId = GetRequiredUserId();
            if (userId == null) return Unauthorized();

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
        [Authorize]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> ReviewContract(
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            if (!IsValidPdf(file))
                return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

            var userId = GetRequiredUserId();
            if (userId == null) return Unauthorized();

            (bool allowed, IActionResult? limitResponse) = await CheckAiUsageAsync(
                userId, "contract-review", OpusModel);
            if (!allowed) return limitResponse!;

            _logger.LogInformation(
                "Contract review request. File: {File}", file.FileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);
            var pdfBytes = ms.ToArray();
            var pageCount = CountPdfPages(pdfBytes);
            var isPro = await IsProUserAsync(userId);

            var result = await _aiService.ReviewContractAsync(
                pdfBytes,
                userId,
                isPro,
                pageCount,
                cancellationToken);

            if (!result.IsSuccess)
            {
                if (result.RequiresUpgrade)
                {
                    return StatusCode(402, new
                    {
                        error = result.ErrorMessage,
                        upgradePath = "/pricing"
                    });
                }

                return UnprocessableEntity(new { error = result.ErrorMessage });
            }

            return Ok(new
            {
                json = result.JsonData,
                ocrFallbackUsed = result.OcrFallbackUsed,
                ocrWarning = result.OcrWarning
            });
        }

        // POST api/ai/ocr
        [HttpPost("ocr")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> OcrPdf(
            IFormFile file,
            [FromQuery] string language = "eng",
            [FromQuery] string processMode = "free-preview",
            [FromQuery] int? maxPages = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsValidPdf(file))
                return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

            _logger.LogInformation(
                "OCR request. Language: {Lang}, File: {File}",
                language, file.FileName);

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, cancellationToken);
                var pdfBytes = ms.ToArray();
                var pageCount = CountPdfPages(pdfBytes);
                var userId = GetOptionalUserId() ?? "anonymous";
                var normalizedLanguage = NormalizeOcrLanguage(language);
                var normalizedProcessMode = NormalizeOcrProcessMode(processMode);
                var premiumRequired =
                    normalizedProcessMode == OcrProcessModeFullDocument ||
                    normalizedLanguage != "eng";
                var isPro = await IsProUserAsync(userId);

                _logger.LogInformation(
                    "OCR validation. UserId: {UserId}, Pages: {PageCount}, Language: {Language}, ProcessMode: {ProcessMode}, PremiumRequired: {PremiumRequired}, IsPro: {IsPro}",
                    userId,
                    pageCount,
                    normalizedLanguage,
                    normalizedProcessMode,
                    premiumRequired,
                    isPro);

                if (normalizedLanguage != "eng" && userId == "anonymous")
                {
                    return StatusCode(403, new
                    {
                        error = "Multilingual OCR requires Pro.",
                        upgradePath = "/pricing"
                    });
                }

                if (normalizedProcessMode == OcrProcessModeFullDocument &&
                    userId == "anonymous" &&
                    pageCount > 3)
                {
                    return StatusCode(402, new
                    {
                        error = "Free OCR supports up to 3 pages. Upgrade to process the full document.",
                        upgradePath = "/pricing"
                    });
                }

                if (premiumRequired && !isPro)
                    return UpgradeRequired();

                var tessDataPath = Path.Combine(
                    AppContext.BaseDirectory, "tessdata");

                var processor = new PdfToolStack.Infrastructure
                    .Processors.PdfOcrProcessor(tessDataPath);

                var pagesToProcess = normalizedProcessMode == OcrProcessModeFreePreview
                    ? 3
                    : (int?)null;

                var result = await processor.ProcessWithInfoAsync(
                    pdfBytes, normalizedLanguage, cancellationToken, pagesToProcess);

                if (result.PdfBytes.Length == 0)
                    return UnprocessableEntity(new
                    {
                        error = "OCR generated an empty PDF. Please try again."
                    });

                _logger.LogInformation(
                    "OCR completed. UserId: {UserId}, Pages: {PageCount}, ProcessedPages: {ProcessedPages}, Language: {Language}, ProcessMode: {ProcessMode}, PremiumRequired: {PremiumRequired}, OutputBytes: {OutputBytes}, HasExtractedText: {HasExtractedText}, ExtractedTextLength: {ExtractedTextLength}",
                    userId,
                    pageCount,
                    result.PageCount,
                    result.Language,
                    normalizedProcessMode,
                    premiumRequired,
                    result.PdfBytes.Length,
                    result.HasExtractedText,
                    result.ExtractedTextLength);

                return File(result.PdfBytes, "application/pdf",
                    Path.GetFileNameWithoutExtension(file.FileName)
                    + "_searchable.pdf");
            }
            catch (PdfToolStack.Infrastructure.Processors.OcrLanguageUnavailableException ex)
            {
                _logger.LogWarning(
                    ex,
                    "OCR unavailable language requested. Language: {Language}",
                    language);

                return BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Invalid OCR language requested. Language: {Language}",
                    language);

                return BadRequest(new { error = "Please choose a valid OCR language." });
            }
            catch (PdfToolStack.Infrastructure.Processors.OcrProcessingException ex)
            {
                _logger.LogWarning(
                    ex,
                    "OCR processing failed. Language: {Language}, File: {File}",
                    language,
                    file.FileName);

                return UnprocessableEntity(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR failed");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST api/ai/translate
        [HttpPost("translate")]
        [Authorize]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> Translate(
            IFormFile file,
            [FromForm] string targetLanguage = "es",
            [FromForm] string languageName = "Spanish",
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("TRANSLATE ENDPOINT HIT - updated code is running");

            string? userId = null;
            int? extractedTextLength = null;
            int? translatedTextLength = null;
            int? outputPdfBytesLength = null;

            try
            {
                if (!IsValidPdf(file))
                    return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

                userId = GetRequiredUserId();
                if (userId == null) return Unauthorized();

                if (!SupportsLatinScriptPdfOutput(targetLanguage, languageName))
                {
                    _logger.LogWarning(
                        "Translate rejected. UserId: {UserId}, FileName: {FileName}, FileSize: {FileSize}, TargetLanguage: {TargetLanguage}, LanguageName: {LanguageName}, TranslatedTextLength: {TranslatedTextLength}, OutputPdfBytesLength: {OutputPdfBytesLength}, FailureReason: {FailureReason}",
                        userId,
                        file.FileName,
                        file.Length,
                        targetLanguage,
                        languageName,
                        translatedTextLength,
                        outputPdfBytesLength,
                        UnsupportedUnicodePdfMessage);

                    return UnprocessableEntity(new
                    {
                        error = UnsupportedUnicodePdfMessage
                    });
                }

                (bool allowed, IActionResult? limitResponse) = await CheckAiUsageAsync(
                    userId, "translate", HaikuModel);
                if (!allowed)
                {
                    _logger.LogWarning(
                        "Translate usage check failed. UserId: {UserId}, FileName: {FileName}, FileSize: {FileSize}, TargetLanguage: {TargetLanguage}, LanguageName: {LanguageName}, TranslatedTextLength: {TranslatedTextLength}, OutputPdfBytesLength: {OutputPdfBytesLength}",
                        userId,
                        file.FileName,
                        file.Length,
                        targetLanguage,
                        languageName,
                        translatedTextLength,
                        outputPdfBytesLength);

                    return limitResponse!;
                }

                _logger.LogInformation(
                    "Translate request. UserId: {UserId}, TargetLanguage: {TargetLanguage}, LanguageName: {LanguageName}, FileName: {FileName}, FileSize: {FileSize}",
                    userId,
                    targetLanguage,
                    languageName,
                    file.FileName,
                    file.Length);

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, cancellationToken);

                var result = await _aiService.TranslateAsync(
                    ms.ToArray(), targetLanguage, languageName, cancellationToken);

                extractedTextLength = result.SourceTextLength;
                translatedTextLength = result.TranslatedText.Length;

                if (!result.IsSuccess)
                {
                    var failureReason = string.IsNullOrWhiteSpace(result.ErrorMessage)
                        ? "Translation failed. Please try again."
                        : result.ErrorMessage;

                    _logger.LogWarning(
                        "Translate failed. UserId: {UserId}, FileName: {FileName}, FileSize: {FileSize}, TargetLanguage: {TargetLanguage}, LanguageName: {LanguageName}, ExtractedTextLength: {ExtractedTextLength}, TranslatedTextLength: {TranslatedTextLength}, OutputPdfBytesLength: {OutputPdfBytesLength}, FailureReason: {FailureReason}",
                        userId,
                        file.FileName,
                        file.Length,
                        targetLanguage,
                        languageName,
                        extractedTextLength,
                        translatedTextLength,
                        outputPdfBytesLength,
                        failureReason);

                    return UnprocessableEntity(new { error = failureReason });
                }

                if (string.IsNullOrWhiteSpace(result.TranslatedText))
                {
                    const string failureReason =
                        "Translation result was empty. Please try again.";

                    _logger.LogWarning(
                        "Translate returned empty output. UserId: {UserId}, FileName: {FileName}, FileSize: {FileSize}, TargetLanguage: {TargetLanguage}, LanguageName: {LanguageName}, ExtractedTextLength: {ExtractedTextLength}, TranslatedTextLength: {TranslatedTextLength}, OutputPdfBytesLength: {OutputPdfBytesLength}, FailureReason: {FailureReason}",
                        userId,
                        file.FileName,
                        file.Length,
                        targetLanguage,
                        languageName,
                        extractedTextLength,
                        translatedTextLength,
                        outputPdfBytesLength,
                        failureReason);

                    return UnprocessableEntity(new { error = failureReason });
                }

                var outputName =
                    $"{Path.GetFileNameWithoutExtension(file.FileName)}" +
                    $"_translated_{targetLanguage}.pdf";

                byte[] pdfBytes;
                try
                {
                    pdfBytes = BuildPdf(
                        result.TranslatedText,
                        $"Translated to {result.LanguageName}");
                    outputPdfBytesLength = pdfBytes.Length;
                }
                catch (Exception ex)
                {
                    const string failureReason =
                        "Translation completed, but PDF generation failed. Please try a different language.";

                    _logger.LogError(
                        ex,
                        "Translate PDF generation failed. UserId: {UserId}, FileName: {FileName}, FileSize: {FileSize}, TargetLanguage: {TargetLanguage}, LanguageName: {LanguageName}, ExtractedTextLength: {ExtractedTextLength}, TranslatedTextLength: {TranslatedTextLength}, OutputPdfBytesLength: {OutputPdfBytesLength}",
                        userId,
                        file.FileName,
                        file.Length,
                        targetLanguage,
                        languageName,
                        extractedTextLength,
                        translatedTextLength,
                        outputPdfBytesLength);

                    return UnprocessableEntity(new { error = failureReason });
                }

                _logger.LogInformation(
                    "Translate completed. UserId: {UserId}, FileName: {FileName}, FileSize: {FileSize}, TargetLanguage: {TargetLanguage}, LanguageName: {LanguageName}, ExtractedTextLength: {ExtractedTextLength}, TranslatedTextLength: {TranslatedTextLength}, OutputPdfBytesLength: {OutputPdfBytesLength}",
                    userId,
                    file.FileName,
                    file.Length,
                    targetLanguage,
                    languageName,
                    extractedTextLength,
                    translatedTextLength,
                    outputPdfBytesLength);

                return File(pdfBytes, "application/pdf", outputName);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Translate PDF failed. UserId: {UserId}, FileName: {FileName}, FileSize: {FileSize}, TargetLanguage: {TargetLanguage}, LanguageName: {LanguageName}, ExtractedTextLength: {ExtractedTextLength}, TranslatedTextLength: {TranslatedTextLength}, OutputPdfBytesLength: {OutputPdfBytesLength}",
                    userId,
                    file?.FileName,
                    file?.Length,
                    targetLanguage,
                    languageName,
                    extractedTextLength,
                    translatedTextLength,
                    outputPdfBytesLength);

                return UnprocessableEntity(new
                {
                    error = "Translate PDF failed. Check API logs for the exact failure."
                });
            }
        }

        // POST api/ai/rewrite
        [HttpPost("rewrite")]
        [Authorize]
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

            var userId = GetRequiredUserId();
            if (userId == null) return Unauthorized();

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
                var planType = status.IsActive ? status.PlanType : "free";
                var (used, limit) = await _usageService.GetUsageAsync(userId, planType);
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

            document.AddTitle(title);
            document.Open();

            var bodyFont = iTextSharp.text.FontFactory.GetFont(
                iTextSharp.text.FontFactory.HELVETICA,
                11,
                iTextSharp.text.Font.NORMAL,
                iTextSharp.text.BaseColor.Black);
            var h1Font = iTextSharp.text.FontFactory.GetFont(
                iTextSharp.text.FontFactory.HELVETICA,
                18,
                iTextSharp.text.Font.BOLD,
                iTextSharp.text.BaseColor.Black);
            var h2Font = iTextSharp.text.FontFactory.GetFont(
                iTextSharp.text.FontFactory.HELVETICA,
                14,
                iTextSharp.text.Font.BOLD,
                iTextSharp.text.BaseColor.Black);
            var tableHeaderFont = iTextSharp.text.FontFactory.GetFont(
                iTextSharp.text.FontFactory.HELVETICA,
                9,
                iTextSharp.text.Font.BOLD,
                iTextSharp.text.BaseColor.Black);
            var tableCellFont = iTextSharp.text.FontFactory.GetFont(
                iTextSharp.text.FontFactory.HELVETICA,
                9,
                iTextSharp.text.Font.NORMAL,
                iTextSharp.text.BaseColor.Black);

            var lines = text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.None);

            var paragraphLines = new List<string>();

            void FlushParagraph()
            {
                if (paragraphLines.Count == 0)
                    return;

                document.Add(new iTextSharp.text.Paragraph(
                    string.Join(" ", paragraphLines), bodyFont)
                {
                    Leading = 15f,
                    SpacingAfter = 8f
                });
                paragraphLines.Clear();
            }

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (string.IsNullOrWhiteSpace(line))
                {
                    FlushParagraph();
                    continue;
                }

                if (IsPipeTableStart(lines, i))
                {
                    FlushParagraph();
                    i = AddPipeTable(
                        document,
                        lines,
                        i,
                        tableHeaderFont,
                        tableCellFont);
                    continue;
                }

                if (TryGetHeading(line, out var level, out var heading))
                {
                    FlushParagraph();
                    document.Add(new iTextSharp.text.Paragraph(
                        heading,
                        level == 1 ? h1Font : h2Font)
                    {
                        SpacingBefore = level == 1 ? 10f : 8f,
                        SpacingAfter = 6f
                    });
                    continue;
                }

                if (TryGetBullet(line, out var bulletText))
                {
                    FlushParagraph();
                    AddIndentedLine(document, $"- {bulletText}", bodyFont);
                    continue;
                }

                if (TryGetNumberedItem(line, out var numberedText))
                {
                    FlushParagraph();
                    AddIndentedLine(document, numberedText, bodyFont);
                    continue;
                }

                paragraphLines.Add(line);
            }

            FlushParagraph();

            document.Close();
            ms.Position = 0;
            return ms.ToArray();
        }

        private static bool TryGetHeading(
            string line,
            out int level,
            out string text)
        {
            level = 0;
            text = string.Empty;

            if (!line.StartsWith("#"))
                return false;

            var count = line.TakeWhile(c => c == '#').Count();
            if (count > 3 ||
                line.Length <= count ||
                line[count] != ' ')
                return false;

            level = count == 1 ? 1 : 2;
            text = line[count..].Trim();
            return text.Length > 0;
        }

        private static bool TryGetBullet(
            string line,
            out string text)
        {
            text = string.Empty;

            if (line.Length <= 2)
                return false;

            if ((line[0] == '-' || line[0] == '*' || line[0] == '•') &&
                char.IsWhiteSpace(line[1]))
            {
                text = line[2..].Trim();
                return text.Length > 0;
            }

            return false;
        }

        private static bool TryGetNumberedItem(
            string line,
            out string text)
        {
            text = string.Empty;
            var index = 0;

            while (index < line.Length && char.IsDigit(line[index]))
                index++;

            if (index == 0 ||
                index + 1 >= line.Length ||
                (line[index] != '.' && line[index] != ')') ||
                !char.IsWhiteSpace(line[index + 1]))
                return false;

            text = line[..(index + 1)] + " " + line[(index + 1)..].Trim();
            return true;
        }

        private static void AddIndentedLine(
            iTextSharp.text.Document document,
            string text,
            iTextSharp.text.Font font)
        {
            document.Add(new iTextSharp.text.Paragraph(text, font)
            {
                IndentationLeft = 18f,
                FirstLineIndent = -10f,
                Leading = 15f,
                SpacingAfter = 4f
            });
        }

        private static bool IsPipeTableStart(string[] lines, int index)
        {
            return index + 1 < lines.Length &&
                IsPipeTableLine(lines[index]) &&
                IsPipeTableSeparator(lines[index + 1]);
        }

        private static bool IsPipeTableLine(string line) =>
            line.Trim().Contains('|');

        private static bool IsPipeTableSeparator(string line)
        {
            var cells = ParsePipeCells(line);
            return cells.Count > 0 &&
                cells.All(cell =>
                    cell.Length > 0 &&
                    cell.All(c => c == '-' ||
                        c == ':' ||
                        char.IsWhiteSpace(c)));
        }

        private static int AddPipeTable(
            iTextSharp.text.Document document,
            string[] lines,
            int startIndex,
            iTextSharp.text.Font headerFont,
            iTextSharp.text.Font cellFont)
        {
            var headerCells = ParsePipeCells(lines[startIndex]);
            var rows = new List<List<string>>();
            var index = startIndex + 2;

            while (index < lines.Length && IsPipeTableLine(lines[index]))
            {
                var row = ParsePipeCells(lines[index]);
                if (row.Count == 0)
                    break;

                rows.Add(row);
                index++;
            }

            var columnCount = Math.Max(
                headerCells.Count,
                rows.Count == 0 ? 0 : rows.Max(r => r.Count));

            if (columnCount == 0)
                return startIndex;

            var table = new iTextSharp.text.pdf.PdfPTable(columnCount)
            {
                WidthPercentage = 100,
                SpacingBefore = 4f,
                SpacingAfter = 10f,
                HeaderRows = 1
            };

            foreach (var cell in PadCells(headerCells, columnCount))
                table.AddCell(CreateTableCell(cell, headerFont, true));

            foreach (var row in rows)
            {
                foreach (var cell in PadCells(row, columnCount))
                    table.AddCell(CreateTableCell(cell, cellFont, false));
            }

            document.Add(table);
            return index - 1;
        }

        private static iTextSharp.text.pdf.PdfPCell CreateTableCell(
            string text,
            iTextSharp.text.Font font,
            bool isHeader)
        {
            return new iTextSharp.text.pdf.PdfPCell(
                new iTextSharp.text.Phrase(text, font))
            {
                Padding = 5f,
                BackgroundColor = isHeader
                    ? new iTextSharp.text.BaseColor(241, 245, 249)
                    : iTextSharp.text.BaseColor.White
            };
        }

        private static List<string> ParsePipeCells(string line)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('|'))
                trimmed = trimmed[1..];
            if (trimmed.EndsWith('|'))
                trimmed = trimmed[..^1];

            return trimmed
                .Split('|')
                .Select(cell => cell.Trim())
                .ToList();
        }

        private static IEnumerable<string> PadCells(
            List<string> cells,
            int count)
        {
            for (var i = 0; i < count; i++)
                yield return i < cells.Count ? cells[i] : string.Empty;
        }

        // GET api/ai/usage/{userId}
        [HttpGet("usage/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetUsage(string userId)
        {
            if (_subscriptionService == null)
                return StatusCode(503, new { error = "Service unavailable." });

            var status = await _subscriptionService.GetStatusAsync(userId);
            if (status?.PlanType == null)
                return Ok(new { used = 0, limit = 5, remaining = 5, percentage = 0 });

            var planType = status.IsActive ? status.PlanType : "free";
            var (used, limit) = await _usageService.GetUsageAsync(
                userId, planType);
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
                if (_subscriptionService != null)
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
                _logger.LogError(ex, "Usage check failed for AI feature {Feature}", feature);
                return (false, StatusCode(503, new
                {
                    error = "AI usage credits could not be verified. Please try again."
                }));
            }
        }

        private string? GetRequiredUserId() =>
            User.FindFirst("sub")?.Value ??
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        private string? GetOptionalUserId() =>
            User.Identity?.IsAuthenticated == true
                ? GetRequiredUserId()
                : null;

        private static bool SupportsLatinScriptPdfOutput(
            string? targetLanguage,
            string? languageName)
        {
            if (!string.IsNullOrWhiteSpace(targetLanguage) &&
                LatinScriptPdfLanguageCodes.Contains(targetLanguage.Trim()))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(languageName) &&
                LatinScriptPdfLanguageNames.Contains(
                    NormalizeLanguageName(languageName));
        }

        private static string NormalizeLanguageName(string languageName)
        {
            var normalized = languageName.Trim().ToLowerInvariant();
            var parenthesisIndex = normalized.IndexOf('(');

            if (parenthesisIndex >= 0)
                normalized = normalized[..parenthesisIndex].Trim();

            return normalized;
        }

        private const string OcrProcessModeFreePreview = "free-preview";
        private const string OcrProcessModeFullDocument = "full-document";

        private static string NormalizeOcrProcessMode(string? processMode)
        {
            if (string.IsNullOrWhiteSpace(processMode))
                return OcrProcessModeFreePreview;

            var normalized = processMode.Trim().ToLowerInvariant();

            return normalized switch
            {
                OcrProcessModeFreePreview => normalized,
                OcrProcessModeFullDocument => normalized,
                _ => throw new ArgumentException(
                    "Please choose a valid OCR process mode.",
                    nameof(processMode))
            };
        }

        private static string NormalizeOcrLanguage(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return "eng";

            return language.Trim().ToLowerInvariant();
        }

        private async Task<bool> IsProUserAsync(string userId)
        {
            if (userId == "anonymous" || _subscriptionService == null)
                return false;

            var status = await _subscriptionService.GetStatusAsync(userId);
            return status.HasPro;
        }

        private IActionResult UpgradeRequired() =>
            StatusCode(402, new
            {
                error = "This OCR feature requires Pro. Upgrade to process the full document.",
                upgradePath = "/pricing"
            });

        internal static int CountPdfPages(byte[] pdfBytes)
        {
            using var reader = new iTextSharp.text.pdf.PdfReader(pdfBytes);
            return reader.NumberOfPages;
        }

        private int? TryCountPdfPages(byte[] pdfBytes)
        {
            try
            {
                return CountPdfPages(pdfBytes);
            }
            catch (Exception ex)
            {
                LogExceptionDetails(
                    ex,
                    "PDF page count failed during invoice extraction. PdfBytes: {PdfBytes}",
                    pdfBytes.Length);
                return null;
            }
        }

        private IActionResult ToExtractionFailureResponse(ExtractResult result)
        {
            var body = new
            {
                error = result.ErrorMessage,
                textPreview = result.TextPreview,
                ocrFallbackUsed = result.OcrFallbackUsed,
                ocrWarning = result.OcrWarning
            };

            return result.FailureKind switch
            {
                ExtractionFailureKind.NoReadableText => BadRequest(body),
                ExtractionFailureKind.RequiresUpgrade => StatusCode(402, body),
                ExtractionFailureKind.AiConfiguration => StatusCode(503, body),
                _ => UnprocessableEntity(body)
            };
        }

        private void LogExceptionDetails(
            Exception ex,
            string message,
            params object?[] args)
        {
            var fullMessage =
                message +
                " ExceptionType: {ExceptionType}, ExceptionMessage: {ExceptionMessage}, InnerException: {InnerException}, StackTrace: {StackTrace}";

            var fullArgs = args
                .Concat(new object?[]
                {
                    ex.GetType().FullName,
                    ex.Message,
                    ex.InnerException?.ToString(),
                    ex.StackTrace
                })
                .ToArray();

            _logger.LogError(ex, fullMessage, fullArgs);
        }

        internal static bool IsValidPdf(IFormFile? file)
        {
            if (file == null || file.Length == 0) return false;
            if (file.Length > 52_428_800) return false;

            using var stream = file.OpenReadStream();
            var header = new byte[4];
            try
            {
                stream.ReadExactly(header);
            }
            catch (EndOfStreamException)
            {
                return false;
            }

            return header[0] == 0x25 && header[1] == 0x50 &&
                   header[2] == 0x44 && header[3] == 0x46;
        }

        internal static byte[] BuildExcel(string json, string type)
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
