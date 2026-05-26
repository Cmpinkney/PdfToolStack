using System.Text.Json;
using PdfToolStack.Application.DTOs;
using PdfToolStack.Application.Interfaces;

namespace PdfToolStack.Application.Services;

public sealed class FormulaGenerationService : IFormulaGenerationService
{
    private const int MaxPromptLength = 2000;
    private readonly IFormulaAiProvider _provider;

    public FormulaGenerationService(IFormulaAiProvider provider)
    {
        _provider = provider;
    }

    public async Task<FormulaResponse> GenerateAsync(
        FormulaRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new ArgumentException("Describe the formula you need.", nameof(request));

        var platform = NormalizePlatform(request.Platform);
        var prompt = request.Prompt.Trim();

        if (prompt.Length > MaxPromptLength)
            throw new ArgumentException("Formula prompt is too long.", nameof(request));

        var rawResponse = await _provider.GenerateFormulaAsync(
            BuildSystemPrompt(platform),
            BuildUserPrompt(prompt, platform),
            cancellationToken);

        return ParseResponse(rawResponse);
    }

    private static string NormalizePlatform(string? platform)
    {
        if (string.IsNullOrWhiteSpace(platform))
            return "Excel";

        var normalized = platform.Trim().ToLowerInvariant();

        return normalized switch
        {
            "excel" => "Excel",
            "google sheets" or "googlesheets" or "sheets" =>
                "Google Sheets",
            _ => throw new ArgumentException(
                "Platform must be Excel or Google Sheets.",
                nameof(platform))
        };
    }

    private static string BuildSystemPrompt(string platform) =>
        $$"""
        You generate {{platform}} formulas from plain English.
        Return ONLY valid JSON with this exact shape:
        {
          "generatedFormula": "=FORMULA(...)",
          "explanation": "One concise sentence explaining what the formula does."
        }
        Rules:
        - Output one formula only.
        - Start the formula with =.
        - Use {{platform}}-compatible functions.
        - Keep the explanation under 35 words.
        - Do not include markdown or code fences.
        """;

    private static string BuildUserPrompt(string prompt, string platform) =>
        $"Platform: {platform}\nRequest: {prompt}";

    private static FormulaResponse ParseResponse(string rawResponse)
    {
        var clean = rawResponse
            .Replace("```json", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```", "")
            .Trim();

        FormulaResponse? response;

        try
        {
            response = JsonSerializer.Deserialize<FormulaResponse>(
                clean,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                "AI formula response was not valid JSON.");
        }

        if (response is null ||
            string.IsNullOrWhiteSpace(response.GeneratedFormula) ||
            string.IsNullOrWhiteSpace(response.Explanation))
        {
            throw new InvalidOperationException(
                "AI formula response was incomplete.");
        }

        var formula = response.GeneratedFormula.Trim();
        if (!formula.StartsWith('='))
            formula = "=" + formula;

        return new FormulaResponse(
            formula,
            response.Explanation.Trim());
    }
}
