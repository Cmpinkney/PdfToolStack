using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ExcelToolStack.Web.Services;

public sealed class FormulaApiService
{
    private readonly HttpClient _http;

    public FormulaApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<FormulaApiResponse> GenerateFormulaAsync(
        FormulaApiRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(
            "api/excel-ai/formula",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await ReadErrorMessageAsync(response, cancellationToken));
        }

        return await response.Content
            .ReadFromJsonAsync<FormulaApiResponse>(cancellationToken)
            ?? throw new InvalidOperationException(
                "Formula generation returned an empty response.");
    }

    private sealed record ApiError(string Error);

    private static async Task<string> ReadErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return "Too many formula requests. Please wait a minute and try again.";
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            return "Formula generation is temporarily unavailable. Please try again shortly.";
        }

        try
        {
            var error = await response.Content
                .ReadFromJsonAsync<ApiError>(cancellationToken);

            if (!string.IsNullOrWhiteSpace(error?.Error))
                return error.Error;
        }
        catch (JsonException)
        {
        }

        return "Formula generation failed. Please try again.";
    }
}

public sealed record FormulaApiRequest(
    string Prompt,
    string Platform);

public sealed record FormulaApiResponse(
    string GeneratedFormula,
    string Explanation);
