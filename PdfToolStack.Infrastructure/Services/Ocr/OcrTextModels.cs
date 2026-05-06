namespace PdfToolStack.Infrastructure.Services.Ocr
{
    public sealed record OcrTextRequest(byte[] PdfBytes)
    {
        public string Language { get; init; } = "eng";
        public int? MaxPages { get; init; }
        public int? TotalPageCount { get; init; }
        public bool HighAccuracy { get; init; }
        public bool AllowGoogleVisionFallback { get; init; }
        public bool RequireCompleteDocument { get; init; }
        public string UserId { get; init; } = "unknown";
        public bool IsAnonymous { get; init; } = true;
        public bool IsProUser { get; init; }
    }

    public sealed record OcrTextBlock(
        int PageNumber,
        string Text,
        float? Confidence);

    public sealed class OcrTextProviderResult
    {
        public string Provider { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public int PageCount { get; init; }
        public int PagesProcessed { get; init; }
        public int ExtractedTextLength { get; init; }
        public float? AverageConfidence { get; init; }
        public IReadOnlyList<OcrTextBlock> Blocks { get; init; } =
            Array.Empty<OcrTextBlock>();

        public bool HasExtractedText => ExtractedTextLength > 0;
    }

    public sealed class SmartOcrTextResult
    {
        public bool IsSuccess =>
            !RequiresUpgrade &&
            string.IsNullOrWhiteSpace(ErrorMessage) &&
            ExtractedTextLength > 0;

        public string Text { get; init; } = string.Empty;
        public string ProviderUsed { get; init; } = string.Empty;
        public bool WasOcrUsed { get; init; }
        public bool WasGoogleVisionFallbackUsed { get; init; }
        public string? FallbackReason { get; init; }
        public int PageCount { get; init; }
        public int PagesProcessed { get; init; }
        public int ExtractedTextLength { get; init; }
        public float? AverageConfidence { get; init; }
        public bool IsPartial { get; init; }
        public bool RequiresUpgrade { get; init; }
        public string? ErrorMessage { get; init; }

        public static SmartOcrTextResult FromProvider(
            OcrTextProviderResult result,
            bool wasGoogleVisionFallbackUsed,
            string? fallbackReason,
            bool isPartial) =>
            new()
            {
                Text = result.Text,
                ProviderUsed = result.Provider,
                WasOcrUsed = true,
                WasGoogleVisionFallbackUsed = wasGoogleVisionFallbackUsed,
                FallbackReason = fallbackReason,
                PageCount = result.PageCount,
                PagesProcessed = result.PagesProcessed,
                ExtractedTextLength = result.ExtractedTextLength,
                AverageConfidence = result.AverageConfidence,
                IsPartial = isPartial
            };

        public static SmartOcrTextResult Failure(
            string message,
            string? fallbackReason,
            int pageCount,
            int pagesProcessed) =>
            new()
            {
                ErrorMessage = message,
                FallbackReason = fallbackReason,
                PageCount = pageCount,
                PagesProcessed = pagesProcessed,
                WasOcrUsed = true
            };

        public static SmartOcrTextResult UpgradeRequired(
            string message,
            string? fallbackReason,
            int pageCount,
            int pagesProcessed) =>
            new()
            {
                ErrorMessage = message,
                RequiresUpgrade = true,
                FallbackReason = fallbackReason,
                PageCount = pageCount,
                PagesProcessed = pagesProcessed,
                WasOcrUsed = true,
                IsPartial = true
            };
    }

    public static class OcrTextQuality
    {
        public static int CountMeaningfulCharacters(string? text) =>
            string.IsNullOrEmpty(text)
                ? 0
                : text.Count(c => !char.IsWhiteSpace(c));
    }
}
