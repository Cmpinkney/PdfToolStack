using Microsoft.Extensions.Logging;
using PdfToolStack.Application.DTOs;
using PdfToolStack.Infrastructure.Services.Ocr;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UglyToad.PdfPig;
using static PdfToolStack.Infrastructure.Services.TranslateResult;

namespace PdfToolStack.Infrastructure.Services
{
    public class AiService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly int _maxTokens;
        private readonly ILogger<AiService> _logger;
        private readonly SmartOcrTextService? _smartOcrTextService;
        // Add these constants inside AiService class
        private const string HaikuModel = "claude-haiku-4-5-20251001";
        private const int MinContractReviewTextChars = 800;
        private const int MinSinglePageContractReviewTextChars = 200;
        private const string ScannedPdfTranslateMessage =
            "This PDF appears to be scanned or image-based. Run OCR first, then translate the searchable PDF.";
        private const string OcrContractReviewWarning =
            "This review was generated from OCR text. Handwriting and poor scans may reduce accuracy.";
        private const string ScannedLowQualityContractMessage =
            "This appears to be a scanned PDF and the text quality is too low for contract review. Try a clearer scan.";
        private const int MinInvoiceExtractionTextChars = 40;
        private const int TextPreviewLength = 1200;
        // _model field stays as Opus for contract review

        public AiService(
            HttpClient http,
            string apiKey,
            string model,
            int maxTokens,
            ILogger<AiService> logger,
            SmartOcrTextService? smartOcrTextService = null)
        {
            _http = http;
            _apiKey = apiKey;
            _model = model;
            _maxTokens = maxTokens;
            _logger = logger;
            _smartOcrTextService = smartOcrTextService;
        }

        public async Task<ExtractResult> ExtractDataAsync(
            byte[] pdfBytes,
            string extractionType,
            CancellationToken cancellationToken = default,
            string userId = "unknown",
            bool isProUser = false,
            int? totalPageCount = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var ocrFallbackUsed = false;
            string? ocrWarning = null;
            string? ocrFailureReason = null;

            _logger.LogInformation(
                "Invoice extraction started. ExtractionType: {ExtractionType}, PdfBytes: {PdfBytes}, PageCount: {PageCount}, IsPro: {IsPro}",
                extractionType,
                pdfBytes.Length,
                totalPageCount,
                isProUser);

            var text = ExtractText(pdfBytes, "invoice-extraction");
            var extractedTextLength =
                OcrTextQuality.CountMeaningfulCharacters(text);

            _logger.LogInformation(
                "Invoice PDF text extraction completed. ExtractionType: {ExtractionType}, ExtractedTextLength: {ExtractedTextLength}, RawTextLength: {RawTextLength}, PageCount: {PageCount}",
                extractionType,
                extractedTextLength,
                text.Length,
                totalPageCount);

            if (extractedTextLength < MinInvoiceExtractionTextChars &&
                _smartOcrTextService != null)
            {
                _logger.LogInformation(
                    "Invoice OCR fallback starting. ExtractionType: {ExtractionType}, ExtractedTextLength: {ExtractedTextLength}, PageCount: {PageCount}, IsPro: {IsPro}",
                    extractionType,
                    extractedTextLength,
                    totalPageCount,
                    isProUser);

                try
                {
                    var smartOcrResult =
                        await _smartOcrTextService.ExtractTextAsync(
                            new OcrTextRequest(pdfBytes)
                            {
                                Language = "eng",
                                TotalPageCount = totalPageCount,
                                AllowGoogleVisionFallback = true,
                                RequireCompleteDocument = false,
                                UserId = userId,
                                IsAnonymous = string.IsNullOrWhiteSpace(userId) ||
                                    userId == "anonymous" ||
                                    userId == "unknown",
                                IsProUser = isProUser
                            },
                            cancellationToken);

                    if (smartOcrResult.RequiresUpgrade)
                    {
                        return ExtractResult.Failure(
                            smartOcrResult.ErrorMessage ??
                            "Upgrade to Pro to extract scanned invoices with OCR fallback.",
                            ExtractionFailureKind.RequiresUpgrade,
                            text,
                            smartOcrResult.WasGoogleVisionFallbackUsed,
                            smartOcrResult.FallbackReason);
                    }

                    if (smartOcrResult.IsSuccess)
                    {
                        text = smartOcrResult.Text;
                        extractedTextLength = smartOcrResult.ExtractedTextLength;
                        ocrFallbackUsed = smartOcrResult.WasGoogleVisionFallbackUsed;
                        ocrWarning = smartOcrResult.IsPartial
                            ? "OCR fallback processed only part of this document."
                            : "OCR fallback was used for this document.";

                        _logger.LogInformation(
                            "Invoice OCR fallback completed. Provider: {Provider}, GoogleFallbackUsed: {GoogleFallbackUsed}, ExtractedTextLength: {ExtractedTextLength}, PagesProcessed: {PagesProcessed}, PageCount: {PageCount}, FallbackReason: {FallbackReason}",
                            smartOcrResult.ProviderUsed,
                            smartOcrResult.WasGoogleVisionFallbackUsed,
                            smartOcrResult.ExtractedTextLength,
                            smartOcrResult.PagesProcessed,
                            smartOcrResult.PageCount,
                            smartOcrResult.FallbackReason);
                    }
                    else
                    {
                        ocrFailureReason = smartOcrResult.FallbackReason;
                        _logger.LogWarning(
                            "Invoice OCR fallback did not produce readable text. Error: {ErrorMessage}, FallbackReason: {FallbackReason}, PageCount: {PageCount}, PagesProcessed: {PagesProcessed}",
                            smartOcrResult.ErrorMessage,
                            smartOcrResult.FallbackReason,
                            smartOcrResult.PageCount,
                            smartOcrResult.PagesProcessed);
                    }
                }
                catch (Exception ex)
                {
                    ocrFailureReason = "ocr-exception";
                    LogExceptionDetails(
                        ex,
                        "Invoice OCR fallback threw. ExtractionType: {ExtractionType}, PageCount: {PageCount}",
                        extractionType,
                        totalPageCount);
                }
            }

            if (OcrTextQuality.CountMeaningfulCharacters(text) <
                MinInvoiceExtractionTextChars)
            {
                return ExtractResult.Failure(
                    "No readable invoice text detected.",
                    ExtractionFailureKind.NoReadableText,
                    text,
                    ocrFallbackUsed,
                    ocrFailureReason ?? ocrWarning);
            }

            // Truncate to avoid token limits (~12K chars ≈ 3K tokens)
            if (text.Length > 12000)
                text = text[..12000] + "\n[Document truncated]";

            var systemPrompt = GetSystemPrompt(extractionType);
            var userPrompt =
                $"Extract structured data from this document:\n\n{text}";

            var requestBody = new
            {
                model = HaikuModel,
                max_tokens = _maxTokens,
                system = systemPrompt,
                messages = new[]
                {
                    new { role = "user", content = userPrompt }
                }
            };

            try
            {
                if (string.IsNullOrWhiteSpace(_apiKey))
                {
                    _logger.LogError(
                        "Invoice extraction AI request blocked because Anthropic:ApiKey is missing or empty. ExtractionType: {ExtractionType}",
                        extractionType);

                    return ExtractResult.Failure(
                        "AI extraction is not configured.",
                        ExtractionFailureKind.AiConfiguration,
                        text,
                        ocrFallbackUsed,
                        ocrWarning);
                }

                var json = JsonSerializer.Serialize(requestBody);
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.anthropic.com/v1/messages");

                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = new StringContent(
                    json, Encoding.UTF8, "application/json");

                _logger.LogInformation(
                    "Invoice AI request sending. ExtractionType: {ExtractionType}, Model: {Model}, MaxTokens: {MaxTokens}, PromptTextLength: {PromptTextLength}, OcrFallbackUsed: {OcrFallbackUsed}",
                    extractionType,
                    HaikuModel,
                    _maxTokens,
                    text.Length,
                    ocrFallbackUsed);

                var response = await _http.SendAsync(
                    request, cancellationToken);

                var responseBody = await response.Content
                    .ReadAsStringAsync(cancellationToken);

                _logger.LogInformation(
                    "Invoice AI response received. ExtractionType: {ExtractionType}, StatusCode: {StatusCode}, ResponseLength: {ResponseLength}, ElapsedMs: {ElapsedMs}",
                    extractionType,
                    (int)response.StatusCode,
                    responseBody.Length,
                    stopwatch.ElapsedMilliseconds);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Anthropic API error during invoice extraction. Status: {Status}, Body: {Body}",
                        response.StatusCode,
                        responseBody);
                    return ExtractResult.Failure(
                        "AI service unavailable. Please try again.",
                        ExtractionFailureKind.AiProvider,
                        text,
                        ocrFallbackUsed,
                        ocrWarning);
                }

                _logger.LogInformation(
                    "Invoice AI response parsing started. ExtractionType: {ExtractionType}, ResponseLength: {ResponseLength}",
                    extractionType,
                    responseBody.Length);

                var parsed = JsonSerializer.Deserialize<AnthropicResponse>(
                    responseBody,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                var content = parsed?.Content?
                    .FirstOrDefault(c => c.Type == "text")?.Text
                    ?? string.Empty;

                // Strip markdown code fences if present
                content = content
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                _logger.LogInformation(
                    "Invoice AI content extracted. ExtractionType: {ExtractionType}, ContentLength: {ContentLength}, Preview: {Preview}",
                    extractionType,
                    content.Length,
                    CreateTextPreview(content, 500));

                using var extractedJson = JsonDocument.Parse(content);

                _logger.LogInformation(
                    "Invoice JSON validation completed. ExtractionType: {ExtractionType}, RootKind: {RootKind}, OcrFallbackUsed: {OcrFallbackUsed}, ElapsedMs: {ElapsedMs}",
                    extractionType,
                    extractedJson.RootElement.ValueKind,
                    ocrFallbackUsed,
                    stopwatch.ElapsedMilliseconds);

                return ExtractResult.Success(
                    content,
                    extractionType,
                    CreateTextPreview(text),
                    ocrFallbackUsed,
                    ocrWarning);
            }
            catch (JsonException ex)
            {
                LogExceptionDetails(
                    ex,
                    "Failed to parse invoice AI JSON response. ExtractionType: {ExtractionType}, TextPreview: {TextPreview}",
                    extractionType,
                    CreateTextPreview(text));
                return ExtractResult.Failure(
                    "Unable to extract structured invoice data from this document.",
                    ExtractionFailureKind.StructuredExtraction,
                    text,
                    ocrFallbackUsed,
                    ocrWarning);
            }
            catch (Exception ex)
            {
                LogExceptionDetails(
                    ex,
                    "AI extraction error. ExtractionType: {ExtractionType}, TextLength: {TextLength}",
                    extractionType,
                    text.Length);
                return ExtractResult.Failure(
                    "Unable to extract structured invoice data from this document.",
                    ExtractionFailureKind.Unexpected,
                    text,
                    ocrFallbackUsed,
                    ocrWarning);
            }
        }

        public async Task<string> SummarizeAsync(
            byte[] pdfBytes,
            CancellationToken cancellationToken = default)
        {
            var text = ExtractText(pdfBytes, "summarize");

            if (string.IsNullOrWhiteSpace(text))
                return "Could not extract text from this PDF.";

            if (text.Length > 12000)
                text = text[..12000] + "\n[Document truncated]";

            var requestBody = new
            {
                model = HaikuModel,
                max_tokens = _maxTokens,
                system = "You are a document summarization expert. " +
                         "Produce clear, concise summaries that capture " +
                         "key points, main arguments, and important conclusions. " +
                         "Use plain language. Structure with short paragraphs.",
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = $"Summarize this document:\n\n{text}"
                    }
                }
            };

            return await CallApiAsync(requestBody, cancellationToken);
        }

        public async Task<ContractReviewResult> ReviewContractAsync(
            byte[] pdfBytes,
            CancellationToken cancellationToken = default)
        {
            return await ReviewContractAsync(
                pdfBytes,
                userId: "unknown",
                isProUser: false,
                totalPageCount: null,
                cancellationToken);
        }

        public async Task<ContractReviewResult> ReviewContractAsync(
            byte[] pdfBytes,
            string userId,
            bool isProUser,
            int? totalPageCount,
            CancellationToken cancellationToken = default,
            bool highAccuracy = false)
        {
            var text = ExtractText(pdfBytes, "contract-review");
            var ocrFallbackUsed = false;
            string? ocrWarning = null;

            if (IsTooShortForContractReview(text, totalPageCount) &&
                _smartOcrTextService != null)
            {
                var smartOcrResult =
                    await _smartOcrTextService.ExtractTextAsync(
                        new OcrTextRequest(pdfBytes)
                        {
                            Language = "eng",
                            TotalPageCount = totalPageCount,
                            HighAccuracy = highAccuracy,
                            AllowGoogleVisionFallback = true,
                            RequireCompleteDocument = true,
                            UserId = userId,
                            IsAnonymous = string.IsNullOrWhiteSpace(userId) ||
                                userId == "anonymous" ||
                                userId == "unknown",
                            IsProUser = isProUser
                        },
                        cancellationToken);

                if (smartOcrResult.RequiresUpgrade)
                {
                    return ContractReviewResult.Failure(
                        smartOcrResult.ErrorMessage ??
                        "Upgrade to Pro to review scanned contracts with OCR fallback.",
                        requiresUpgrade: true);
                }

                if (smartOcrResult.IsSuccess)
                {
                    text = smartOcrResult.Text;
                    ocrFallbackUsed = smartOcrResult.WasOcrUsed;
                    ocrWarning = OcrContractReviewWarning;
                }
            }

            if (IsTooShortForContractReview(text, totalPageCount))
                return ContractReviewResult.Failure(
                    ScannedLowQualityContractMessage);

            if (text.Length > 16000)
                text = text[..16000] + "\n[Document truncated]";

            var systemPrompt = """
        You are an expert contract reviewer. Analyze the contract and return a JSON object with this exact structure:
        {
          "summary": "2-3 sentence plain English summary of what this contract is about",
          "riskLevel": "low|medium|high",
          "keyDates": [{"label": "string", "date": "string", "importance": "string"}],
          "parties": [{"role": "string", "name": "string"}],
          "obligations": [{"party": "string", "obligation": "string", "critical": true|false}],
          "riskyClauses": [{"title": "string", "excerpt": "string", "risk": "string", "severity": "low|medium|high"}],
          "missingElements": ["string"],
          "recommendations": ["string"]
        }
        Return ONLY valid JSON. No markdown, no explanation.
        """;

            var userPrompt = $"Review this contract:\n\n{text}";

            var requestBody = new
            {
                model = _model,
                max_tokens = 4000,
                system = systemPrompt,
                messages = new[] { new { role = "user", content = userPrompt } }
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(
                HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(json,
                Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(request, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return ContractReviewResult.Failure(
                    $"AI service error: {response.StatusCode}");

            using var doc = JsonDocument.Parse(responseJson);
            var content = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? "";

            // Strip markdown if present
            content = content.Trim();
            if (content.StartsWith("```json"))
                content = content[7..];
            if (content.StartsWith("```"))
                content = content[3..];
            if (content.EndsWith("```"))
                content = content[..^3];

            return ContractReviewResult.Success(
                content.Trim(),
                ocrFallbackUsed,
                ocrWarning);
        }

        public class ContractReviewResult
        {
            public bool IsSuccess { get; private set; }
            public string JsonData { get; private set; } = string.Empty;
            public string? ErrorMessage { get; private set; }
            public bool OcrFallbackUsed { get; private set; }
            public string? OcrWarning { get; private set; }
            public bool RequiresUpgrade { get; private set; }

            public static ContractReviewResult Success(
                string json,
                bool ocrFallbackUsed = false,
                string? ocrWarning = null) =>
                new()
                {
                    IsSuccess = true,
                    JsonData = json,
                    OcrFallbackUsed = ocrFallbackUsed,
                    OcrWarning = ocrWarning
                };

            public static ContractReviewResult Failure(
                string error,
                bool requiresUpgrade = false) =>
                new()
                {
                    IsSuccess = false,
                    ErrorMessage = error,
                    RequiresUpgrade = requiresUpgrade
                };
        }

        public async Task<string> ChatAsync(
            byte[] pdfBytes,
            string question,
            CancellationToken cancellationToken = default)
        {
            var text = ExtractText(pdfBytes, "chat");

            if (string.IsNullOrWhiteSpace(text))
                return "Could not extract text from this PDF.";

            if (text.Length > 12000)
                text = text[..12000] + "\n[Document truncated]";

            var requestBody = new
            {
                model = HaikuModel,
                max_tokens = _maxTokens,
                system = "You are a helpful assistant that answers questions " +
                         "about documents. Only use information from the " +
                         "provided document. If the answer isn't in the " +
                         "document, say so clearly.",
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = $"Document:\n\n{text}\n\n" +
                                  $"Question: {question}"
                    }
                }
            };

            return await CallApiAsync(requestBody, cancellationToken);
        }

        public async Task<TranslateResult> TranslateAsync(
            byte[] pdfBytes,
            string targetLanguage,
            string languageName,
            CancellationToken cancellationToken = default)
        {
            var text = ExtractText(pdfBytes, "translate");
            var sourceTextLength = text.Length;
            var meaningfulChars = OcrTextQuality.CountMeaningfulCharacters(text);

            _logger.LogInformation(
                "Translate text extraction. TargetLanguage: {TargetLanguage}, LanguageName: {LanguageName}, TextLength: {TextLength}, MeaningfulChars: {MeaningfulChars}",
                targetLanguage, languageName, sourceTextLength, meaningfulChars);

            if (string.IsNullOrWhiteSpace(text) || meaningfulChars == 0)
            {
                _logger.LogWarning(
                    "Translate rejected — no readable text. TargetLanguage: {TargetLanguage}, TextLength: {TextLength}, MeaningfulChars: {MeaningfulChars}",
                    targetLanguage, sourceTextLength, meaningfulChars);
                return TranslateResult.Failure(
                    ScannedPdfTranslateMessage,
                    sourceTextLength);
            }

            if (text.Length > 14000)
                text = text[..14000] + "\n[Document truncated]";

            var requestBody = new
            {
                model = HaikuModel,
                max_tokens = 4000,
                system = $"""
            You are a professional document translator.
            Translate the provided document text into {languageName}.
            Return structured Markdown so it can be rendered as a readable PDF.
            Rules:
            - Translate ALL text faithfully and completely
            - Preserve document structure with Markdown:
              - Use # for the document title or main heading
              - Use ## for section headings and subheadings
              - Use bullets and numbered lists where the source uses lists
              - Use simple Markdown pipe tables when the source has tabular data
              - Keep paragraph spacing with blank lines between blocks
            - Keep numbers, dates, names, and proper nouns unchanged
            - Do NOT wrap the response in code fences
            - Do NOT add explanations or commentary
            - Return ONLY the translated Markdown, nothing else
            """,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = $"Translate this document into {languageName}:\n\n{text}"
                    }
                }
            };

            _logger.LogInformation(
                "Translate AI request sending. TargetLanguage: {TargetLanguage}, LanguageName: {LanguageName}, TextLength: {TextLength}, Model: {Model}, MaxTokens: 4000",
                targetLanguage, languageName, text.Length, HaikuModel);

            var translated = await CallApiAsync(requestBody, cancellationToken, timeoutSeconds: 90);
            translated = translated
                .Replace("```markdown", "")
                .Replace("```", "")
                .Trim();

            _logger.LogInformation(
                "Translate AI response received. TargetLanguage: {TargetLanguage}, TranslatedLength: {TranslatedLength}",
                targetLanguage, translated.Length);

            if (string.IsNullOrWhiteSpace(translated))
            {
                _logger.LogWarning(
                    "Translate AI returned empty result. TargetLanguage: {TargetLanguage}, LanguageName: {LanguageName}",
                    targetLanguage, languageName);
                return TranslateResult.Failure(
                    "Translation result was empty. Please try again.",
                    sourceTextLength);
            }

            if (translated.StartsWith("AI service"))
            {
                _logger.LogWarning(
                    "Translate AI call failed. TargetLanguage: {TargetLanguage}, LanguageName: {LanguageName}, Response: {Response}",
                    targetLanguage, languageName, translated);
                return TranslateResult.Failure(
                    "Translation failed. Please try again.",
                    sourceTextLength);
            }

            return TranslateResult.Success(
                translated,
                targetLanguage,
                languageName,
                sourceTextLength);
        }

        public async Task<RewriteResult> RewriteAsync(
            byte[] pdfBytes,
            string instruction,
            string tone,
            CancellationToken cancellationToken = default)
        {
            var text = ExtractText(pdfBytes, "rewrite");

            if (string.IsNullOrWhiteSpace(text))
                return RewriteResult.Failure(
                    "Could not extract text from this PDF. " +
                    "It may be a scanned image — try OCR first.");

            if (text.Length > 14000)
                text = text[..14000] + "\n[Document truncated]";

            var toneInstruction = tone switch
            {
                "formal" => "Use formal, professional language.",
                "casual" => "Use friendly, conversational language.",
                "concise" => "Make it concise — remove all unnecessary words.",
                "detailed" => "Expand with more detail and explanation.",
                "persuasive" => "Make it persuasive and compelling.",
                _ => string.Empty
            };

            var requestBody = new
            {
                model = HaikuModel,
                max_tokens = 4000,
                system = $"""
            You are a professional document editor.
            Rewrite the provided document text according to the user's instruction.
            {toneInstruction}
            Rules:
            - Apply the instruction faithfully throughout the entire document
            - Preserve the document structure (headings, paragraphs, lists)
            - Keep names, dates, numbers, and proper nouns unchanged
            - Do NOT add commentary or explanations
            - Return ONLY the rewritten document text
            """,
                messages = new[]
                {
            new
            {
                role = "user",
                content = $"Instruction: {instruction}\n\nDocument:\n\n{text}"
            }
        }
            };

            var rewritten = await CallApiAsync(requestBody, cancellationToken);

            if (string.IsNullOrWhiteSpace(rewritten) ||
                rewritten.StartsWith("AI service"))
                return RewriteResult.Failure("Rewrite failed. Please try again.");

            return RewriteResult.Success(rewritten);
        }

        public async Task<string> SupportChatAsync(
            string userMessage,
            List<SupportMessage>? history = null,
            CancellationToken cancellationToken = default)
        {
            var systemPrompt = """
            You are a friendly, helpful support assistant for PdfToolStack.com —
            an AI-powered PDF tools platform for professionals.

            You help users with:
            - How to use any of the 35+ PDF tools (compress, merge, split, convert,
              sign, edit, annotate, redact, protect, OCR, watermark, rotate, crop,
              number pages, flatten, organize, extract pages, delete pages)
            - AI tools: AI Summarizer, Chat with PDF, AI Contract Reviewer,
              AI Invoice Data Extractor, AI Questions Generator, AI Rewriter,
              PDF Translate
            - Pricing: Free (25MB, all standard tools, no AI), Pro $19/month
              (500MB, AI tools, batch, compare, OCR, cloud storage),
              Teams $49/month (5 seats, shared workspace),
              Developer API $49/month (1000 calls/month)
            - File size limits: Free = 25MB, Pro = 500MB
            - Cloud storage: Google Drive and Dropbox supported on Pro
            - Batch processing: up to 20 files at once, ZIP output, Pro only
            - PDF Compare: word-by-word diff report, Pro only
            - Sign PDF: draw or type signature, place anywhere, free
            - Privacy: files deleted within 1 hour, no data training, no ads on Pro
            - Account: users can manage subscription at /account
            - Billing: managed via Stripe, cancel anytime at /account

            Rules:
            - Be concise and friendly — 2-3 sentences max unless more detail is needed
            - If asked about a specific tool, explain exactly how to use it
            - If the question is about billing, account deletion, or a technical
              error you cannot resolve, tell them to email support@pdftoolstack.com
            - Never make up features that don't exist
            - If unsure, say so and suggest support@pdftoolstack.com
            - Do not discuss competitors
            """;

            var messages = new List<object>();

            if (history != null)
            {
                foreach (var h in history.TakeLast(10))
                    messages.Add(new { role = h.Role, content = h.Content });
            }

            messages.Add(new { role = "user", content = userMessage });

            var requestBody = new
            {
                model = HaikuModel,
                max_tokens = 512,
                system = systemPrompt,
                messages
            };

            return await CallApiAsync(requestBody, cancellationToken);
        }

        private async Task<string> CallApiAsync(
            object requestBody,
            CancellationToken cancellationToken,
            int timeoutSeconds = 30)
        {
            try
            {
                var json = JsonSerializer.Serialize(requestBody);
                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.anthropic.com/v1/messages");

                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = new StringContent(
                    json, Encoding.UTF8, "application/json");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                var response = await _http.SendAsync(request, cts.Token);
                var responseBody = await response.Content
                    .ReadAsStringAsync(cts.Token);

                if (!response.IsSuccessStatusCode)
                    return "AI service unavailable. Please try again.";

                var parsed = JsonSerializer.Deserialize<AnthropicResponse>(
                    responseBody,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return parsed?.Content?
                    .FirstOrDefault(c => c.Type == "text")?.Text
                    ?? string.Empty;
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex, "AI API call failed");
                return "AI service error. Please try again.";
            }
        }

        private string ExtractText(byte[] pdfBytes, string operation)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var doc = PdfDocument.Open(pdfBytes);
                var sb = new StringBuilder();
                var pageCount = 0;
                foreach (var page in doc.GetPages())
                {
                    pageCount++;
                    sb.AppendLine(string.Join(" ",
                        page.GetWords().Select(w => w.Text)));
                    sb.AppendLine();
                }

                var text = sb.ToString().Trim();

                _logger.LogInformation(
                    "PDF text extraction completed. Operation: {Operation}, PageCount: {PageCount}, TextLength: {TextLength}, MeaningfulCharacters: {MeaningfulCharacters}, ElapsedMs: {ElapsedMs}",
                    operation,
                    pageCount,
                    text.Length,
                    OcrTextQuality.CountMeaningfulCharacters(text),
                    stopwatch.ElapsedMilliseconds);

                return text;
            }
            catch (Exception ex)
            {
                LogExceptionDetails(
                    ex,
                    "PDF text extraction failed. Operation: {Operation}, PdfBytes: {PdfBytes}",
                    operation,
                    pdfBytes.Length);
                return string.Empty;
            }
        }

        private static bool IsTooShortForContractReview(
            string? text,
            int? pageCount)
        {
            var meaningfulCharacters =
                OcrTextQuality.CountMeaningfulCharacters(text);

            if (meaningfulCharacters == 0)
                return true;

            var threshold = pageCount.GetValueOrDefault(1) > 1
                ? MinContractReviewTextChars
                : MinSinglePageContractReviewTextChars;

            return meaningfulCharacters < threshold;
        }

        private static string GetSystemPrompt(string extractionType) =>
            extractionType switch
            {
                "invoice" =>
                    "You are a data extraction specialist. Extract structured " +
                    "data from invoice documents and return ONLY valid JSON " +
                    "with no markdown, no explanation, no preamble. " +
                    "Use this exact schema:\n" +
                    "{\n" +
                    "  \"vendor\": string,\n" +
                    "  \"invoiceNumber\": string,\n" +
                    "  \"invoiceDate\": string,\n" +
                    "  \"dueDate\": string,\n" +
                    "  \"totalAmount\": string,\n" +
                    "  \"taxAmount\": string,\n" +
                    "  \"currency\": string,\n" +
                    "  \"lineItems\": [\n" +
                    "    { \"description\": string, \"quantity\": string, " +
                    "\"unitPrice\": string, \"total\": string }\n" +
                    "  ],\n" +
                    "  \"billTo\": string,\n" +
                    "  \"notes\": string\n" +
                    "}\n" +
                    "Use null for missing fields. Never guess.",

                "contract" =>
                    "You are a contract analysis specialist. Extract key " +
                    "information from contracts and return ONLY valid JSON " +
                    "with no markdown, no explanation, no preamble. " +
                    "Use this exact schema:\n" +
                    "{\n" +
                    "  \"parties\": [string],\n" +
                    "  \"effectiveDate\": string,\n" +
                    "  \"expiryDate\": string,\n" +
                    "  \"contractType\": string,\n" +
                    "  \"keyObligations\": [string],\n" +
                    "  \"paymentTerms\": string,\n" +
                    "  \"terminationClause\": string,\n" +
                    "  \"governingLaw\": string,\n" +
                    "  \"renewalTerms\": string\n" +
                    "}\n" +
                    "Use null for missing fields. Never guess.",

                _ =>
                    "You are a data extraction specialist. Extract all " +
                    "structured data from this document and return ONLY " +
                    "valid JSON with no markdown, no explanation, no preamble. " +
                    "Identify the document type and extract all key fields " +
                    "as a flat or nested JSON object. Use null for missing fields."
            };

        // Anthropic response shape
        private class AnthropicResponse
        {
            public List<AnthropicContent>? Content { get; set; }
        }

        private class AnthropicContent
        {
            public string Type { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
        }

        private static string CreateTextPreview(
            string? text,
            int maxLength = TextPreviewLength)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var normalized = string.Join(
                " ",
                text.Split(
                    [' ', '\r', '\n', '\t'],
                    StringSplitOptions.RemoveEmptyEntries));

            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength];
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
    }

    public record SupportMessage(string Role, string Content);

    public enum ExtractionFailureKind
    {
        None,
        NoReadableText,
        StructuredExtraction,
        AiConfiguration,
        AiProvider,
        RequiresUpgrade,
        Unexpected
    }

    public class ExtractResult
    {
        public bool IsSuccess { get; private set; }
        public string JsonData { get; private set; } = string.Empty;
        public string ExtractionType { get; private set; } = string.Empty;
        public string? ErrorMessage { get; private set; }
        public string TextPreview { get; private set; } = string.Empty;
        public bool OcrFallbackUsed { get; private set; }
        public string? OcrWarning { get; private set; }
        public ExtractionFailureKind FailureKind { get; private set; }

        public static ExtractResult Success(
            string json,
            string type,
            string textPreview = "",
            bool ocrFallbackUsed = false,
            string? ocrWarning = null) =>
            new()
            {
                IsSuccess = true,
                JsonData = json,
                ExtractionType = type,
                TextPreview = textPreview,
                OcrFallbackUsed = ocrFallbackUsed,
                OcrWarning = ocrWarning
            };

        public static ExtractResult Failure(
            string error,
            ExtractionFailureKind failureKind = ExtractionFailureKind.Unexpected,
            string? textPreviewSource = null,
            bool ocrFallbackUsed = false,
            string? ocrWarning = null) =>
            new()
            {
                IsSuccess = false,
                ErrorMessage = error,
                FailureKind = failureKind,
                TextPreview = CreatePreview(textPreviewSource),
                OcrFallbackUsed = ocrFallbackUsed,
                OcrWarning = ocrWarning
            };

        private static string CreatePreview(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var normalized = string.Join(
                " ",
                text.Split(
                    [' ', '\r', '\n', '\t'],
                    StringSplitOptions.RemoveEmptyEntries));

            return normalized.Length <= 1200
                ? normalized
                : normalized[..1200];
        }
    }

    public class TranslateResult
    {
        public bool IsSuccess { get; private set; }
        public string TranslatedText { get; private set; } = string.Empty;
        public string TargetLanguage { get; private set; } = string.Empty;
        public string LanguageName { get; private set; } = string.Empty;
        public int SourceTextLength { get; private set; }
        public string? ErrorMessage { get; private set; }

        public static TranslateResult Success(
            string text,
            string lang,
            string name,
            int sourceTextLength) =>
            new()
            {
                IsSuccess = true,
                TranslatedText = text,
                TargetLanguage = lang,
                LanguageName = name,
                SourceTextLength = sourceTextLength
            };

        public static TranslateResult Failure(
            string error,
            int sourceTextLength = 0) =>
            new()
            {
                IsSuccess = false,
                ErrorMessage = error,
                SourceTextLength = sourceTextLength
            };

        public class RewriteResult
        {
            public bool IsSuccess { get; private set; }
            public string RewrittenText { get; private set; } = string.Empty;
            public string? ErrorMessage { get; private set; }

            public static RewriteResult Success(string text) =>
                new() { IsSuccess = true, RewrittenText = text };

            public static RewriteResult Failure(string error) =>
                new() { IsSuccess = false, ErrorMessage = error };
        }
    }
}
