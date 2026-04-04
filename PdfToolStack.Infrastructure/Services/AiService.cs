using Microsoft.Extensions.Logging;
using PdfToolStack.Application.DTOs;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UglyToad.PdfPig;

namespace PdfToolStack.Infrastructure.Services
{
    public class AiService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly int _maxTokens;
        private readonly ILogger<AiService> _logger;
        // Add these constants inside AiService class
        private const string HaikuModel = "claude-haiku-4-5-20251001";
        // _model field stays as Opus for contract review

        public AiService(
            HttpClient http,
            string apiKey,
            string model,
            int maxTokens,
            ILogger<AiService> logger)
        {
            _http = http;
            _apiKey = apiKey;
            _model = model;
            _maxTokens = maxTokens;
            _logger = logger;
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
    CancellationToken cancellationToken = default)
        {
            var text = ExtractText(pdfBytes);

            if (string.IsNullOrWhiteSpace(text))
                return ContractReviewResult.Failure(
                    "Could not extract text from this PDF. " +
                    "It may be a scanned image — try OCR first.");

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

            return ContractReviewResult.Success(content.Trim());
        }

        public class ContractReviewResult
        {
            public bool IsSuccess { get; private set; }
            public string JsonData { get; private set; } = string.Empty;
            public string? ErrorMessage { get; private set; }

            public static ContractReviewResult Success(string json) =>
                new() { IsSuccess = true, JsonData = json };

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

                var response = await _http.SendAsync(
                    request, cancellationToken);
                var responseBody = await response.Content
                    .ReadAsStringAsync(cancellationToken);

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
}