using Microsoft.Extensions.Logging;
using PdfToolStack.Application.DTOs;
using PdfToolStack.Infrastructure.Services.Ocr;
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
            CancellationToken cancellationToken = default)
        {
            // Extract text from PDF using PdfPig
            var text = ExtractText(pdfBytes);

            if (string.IsNullOrWhiteSpace(text))
                return ExtractResult.Failure(
                    "Could not extract text from this PDF. " +
                    "It may be a scanned image — try OCR first.");

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

            var json = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.anthropic.com/v1/messages");

            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(
                json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _http.SendAsync(
                    request, cancellationToken);

                var responseBody = await response.Content
                    .ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Anthropic API error {Status}: {Body}",
                        response.StatusCode, responseBody);
                    return ExtractResult.Failure(
                        "AI service unavailable. Please try again.");
                }

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

                // Validate it's parseable JSON
                JsonDocument.Parse(content);

                return ExtractResult.Success(content, extractionType);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse AI JSON response");
                return ExtractResult.Failure(
                    "AI returned unexpected output. Please try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI extraction error");
                return ExtractResult.Failure($"Extraction failed: {ex.Message}");
            }
        }

        public async Task<string> SummarizeAsync(
            byte[] pdfBytes,
            CancellationToken cancellationToken = default)
        {
            var text = ExtractText(pdfBytes);

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
            string userId = "unknown",
            bool isProUser = false,
            CancellationToken cancellationToken = default)
        {
            var text = ExtractText(pdfBytes);
            string? ocrWarning = null;

            if (IsLowQualityContractText(text, pdfBytes))
            {
                if (_smartOcrTextService == null)
                {
                    return ContractReviewResult.Failure(
                        "This appears to be a scanned PDF and the text quality is too low for contract review. Try a clearer scan.");
                }

                var ocrResult = await _smartOcrTextService.ExtractTextAsync(
                    pdfBytes,
                    new OcrRequestContext(userId, isProUser),
                    cancellationToken);

                if (!ocrResult.HasUsableText ||
                    IsLowQualityContractText(ocrResult.Text, pdfBytes))
                {
                    _logger.LogInformation(
                        "Contract review OCR text too low quality. Provider: {Provider}, PageCount: {PageCount}, PagesProcessed: {PagesProcessed}, ExtractedTextLength: {ExtractedTextLength}, FallbackReason: {FallbackReason}, UserId: {UserId}, IsPro: {IsPro}",
                        ocrResult.Provider,
                        ocrResult.PageCount,
                        ocrResult.PagesProcessed,
                        ocrResult.ExtractedTextLength,
                        ocrResult.FallbackReason,
                        userId,
                        isProUser);

                    return ContractReviewResult.Failure(
                        "This appears to be a scanned PDF and the text quality is too low for contract review. Try a clearer scan.");
                }

                text = ocrResult.Text;
                ocrWarning = ocrResult.FallbackUsed
                    ? ocrResult.Warning ??
                      "This review was generated from OCR text. Handwriting and poor scans may reduce accuracy."
                    : null;
            }

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

            return ContractReviewResult.Success(content.Trim(), ocrWarning);
        }

        public class ContractReviewResult
        {
            public bool IsSuccess { get; private set; }
            public string JsonData { get; private set; } = string.Empty;
            public string? ErrorMessage { get; private set; }
            public string? Warning { get; private set; }

            public static ContractReviewResult Success(
                string json,
                string? warning = null) =>
                new() { IsSuccess = true, JsonData = json, Warning = warning };

            public static ContractReviewResult Failure(string error) =>
                new() { IsSuccess = false, ErrorMessage = error };
        }

        public async Task<string> ChatAsync(
            byte[] pdfBytes,
            string question,
            CancellationToken cancellationToken = default)
        {
            var text = ExtractText(pdfBytes);

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
            var text = ExtractText(pdfBytes);

            if (string.IsNullOrWhiteSpace(text))
                return TranslateResult.Failure(
                    "Could not extract text from this PDF. " +
                    "It may be a scanned image — try OCR first.");

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

            var translated = await CallApiAsync(requestBody, cancellationToken);
            translated = translated
                .Replace("```markdown", "")
                .Replace("```", "")
                .Trim();

            if (string.IsNullOrWhiteSpace(translated) ||
                translated.StartsWith("AI service"))
                return TranslateResult.Failure(
                    "Translation failed. Please try again.");

            return TranslateResult.Success(translated, targetLanguage, languageName);
        }

        public async Task<RewriteResult> RewriteAsync(
            byte[] pdfBytes,
            string instruction,
            string tone,
            CancellationToken cancellationToken = default)
        {
            var text = ExtractText(pdfBytes);

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
            CancellationToken cancellationToken)
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
                cts.CancelAfter(TimeSpan.FromSeconds(30));

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
                _logger.LogError(ex, "AI API call failed");
                return "AI service error. Please try again.";
            }
        }

        private static string ExtractText(byte[] pdfBytes)
        {
            try
            {
                using var doc = PdfDocument.Open(pdfBytes);
                var sb = new StringBuilder();
                foreach (var page in doc.GetPages())
                {
                    sb.AppendLine(string.Join(" ",
                        page.GetWords().Select(w => w.Text)));
                    sb.AppendLine();
                }
                return sb.ToString().Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsLowQualityContractText(
            string? text,
            byte[] pdfBytes)
        {
            if (string.IsNullOrWhiteSpace(text))
                return true;

            if (OcrTextResult.CountMeaningfulCharacters(text) >= 800)
                return false;

            try
            {
                using var doc = PdfDocument.Open(pdfBytes);
                return doc.GetPages().Take(2).Count() > 1;
            }
            catch
            {
                return true;
            }
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
    }

    public record SupportMessage(string Role, string Content);
    public class ExtractResult
    {
        public bool IsSuccess { get; private set; }
        public string JsonData { get; private set; } = string.Empty;
        public string ExtractionType { get; private set; } = string.Empty;
        public string? ErrorMessage { get; private set; }

        public static ExtractResult Success(string json, string type) =>
            new() { IsSuccess = true, JsonData = json, ExtractionType = type };

        public static ExtractResult Failure(string error) =>
            new() { IsSuccess = false, ErrorMessage = error };
    }

    public class TranslateResult
    {
        public bool IsSuccess { get; private set; }
        public string TranslatedText { get; private set; } = string.Empty;
        public string TargetLanguage { get; private set; } = string.Empty;
        public string LanguageName { get; private set; } = string.Empty;
        public string? ErrorMessage { get; private set; }

        public static TranslateResult Success(
            string text, string lang, string name) =>
            new()
            {
                IsSuccess = true,
                TranslatedText = text,
                TargetLanguage = lang,
                LanguageName = name
            };

        public static TranslateResult Failure(string error) =>
            new() { IsSuccess = false, ErrorMessage = error };

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
