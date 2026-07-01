using System.Text.Json;
using Microsoft.JSInterop;

namespace ExcelToolStack.Web.Services;

public sealed class FormulaUsageService
{
    public const int AnonymousMonthlyLimit = 5;
    public const int AuthenticatedMonthlyPreviewLimit = 25;

    private const string AnonymousStorageKey = "ExcelToolStack.formulaUsage.anonymous";
    private const string AuthenticatedStorageKey = "ExcelToolStack.formulaUsage.authenticated";

    private readonly IJSRuntime _js;

    public FormulaUsageService(IJSRuntime js)
    {
        _js = js;
    }

    public event Action? Changed;

    public async Task<FormulaUsageSnapshot> GetSnapshotAsync(bool isAuthenticated)
    {
        var state = await LoadStateAsync(isAuthenticated);
        return CreateSnapshot(state, isAuthenticated);
    }

    public async Task<FormulaUsageSnapshot> RecordGenerationAsync(bool isAuthenticated)
    {
        var state = await LoadStateAsync(isAuthenticated);
        state = state with { Used = state.Used + 1 };
        await SaveStateAsync(isAuthenticated, state);

        var snapshot = CreateSnapshot(state, isAuthenticated);
        Changed?.Invoke();
        return snapshot;
    }

    private async Task<FormulaUsageState> LoadStateAsync(bool isAuthenticated)
    {
        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");

        try
        {
            var json = await _js.InvokeAsync<string?>(
                "localStorage.getItem",
                GetStorageKey(isAuthenticated));

            if (!string.IsNullOrWhiteSpace(json))
            {
                var state = JsonSerializer.Deserialize<FormulaUsageState>(json);
                if (state?.Month == currentMonth)
                {
                    return state;
                }
            }
        }
        catch
        {
        }

        return new FormulaUsageState(currentMonth, 0);
    }

    private async Task SaveStateAsync(bool isAuthenticated, FormulaUsageState state)
    {
        try
        {
            await _js.InvokeVoidAsync(
                "localStorage.setItem",
                GetStorageKey(isAuthenticated),
                JsonSerializer.Serialize(state));
        }
        catch
        {
        }
    }

    private static FormulaUsageSnapshot CreateSnapshot(
        FormulaUsageState state,
        bool isAuthenticated)
    {
        var limit = isAuthenticated
            ? AuthenticatedMonthlyPreviewLimit
            : AnonymousMonthlyLimit;

        return new FormulaUsageSnapshot(
            Used: state.Used,
            Limit: limit,
            Month: state.Month,
            IsAuthenticated: isAuthenticated,
            IsLocalPreview: true);
    }

    private static string GetStorageKey(bool isAuthenticated) =>
        isAuthenticated ? AuthenticatedStorageKey : AnonymousStorageKey;

    private sealed record FormulaUsageState(string Month, int Used);
}

public sealed record FormulaUsageSnapshot(
    int Used,
    int Limit,
    string Month,
    bool IsAuthenticated,
    bool IsLocalPreview)
{
    public int Remaining => Math.Max(0, Limit - Used);

    public double Percentage => Limit <= 0
        ? 0
        : Math.Min(100, Used * 100.0 / Limit);
}
