using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PdfToolStack.Application.Interfaces;

namespace PdfToolStack.Infrastructure.Services;

public sealed class AnthropicFormulaAiProvider : IFormulaAiProvider
{
    private const string DefaultModel = "claude-haiku-4-5-20251001";
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AnthropicFormulaAiProvider> _logger;

    public AnthropicFormulaAiProvider(
        HttpClient http,
        IConfiguration configuration,
        ILogger<AnthropicFormulaAiProvider> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GenerateFormulaAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("AI formula generation is not configured.");

        var model = _configuration["Anthropic:FormulaModel"] ?? DefaultModel;

        var requestBody = new
        {
            model,
            max_tokens = 300,
            temperature = 0,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.anthropic.com/v1/messages");

        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var response = await _http.SendAsync(request, cts.Token);
        var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Formula AI provider returned {StatusCode}: {Body}",
                (int)response.StatusCode,
                responseBody);

            throw new InvalidOperationException(
                "AI formula generation is temporarily unavailable.");
        }

        AnthropicResponse? parsed;

        try
        {
            parsed = JsonSerializer.Deserialize<AnthropicResponse>(
                responseBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Formula AI provider returned malformed JSON.");

            throw new InvalidOperationException(
                "AI formula generation returned an invalid response.");
        }

        return parsed?.Content?
            .FirstOrDefault(content => content.Type == "text")?.Text
            ?? string.Empty;
    }

    private sealed class AnthropicResponse
    {
        public List<AnthropicContent>? Content { get; set; }
    }

    private sealed class AnthropicContent
    {
        public string Type { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
