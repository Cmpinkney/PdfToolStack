namespace PdfToolStack.Infrastructure.Services.Ocr
{
    public sealed record OcrTextResult(
        string Provider,
        string Text,
        int PageCount,
        int PagesProcessed,
        int ExtractedTextLength,
        bool FallbackUsed,
        string? FallbackReason = null,
        string? Warning = null,
        string? ErrorMessage = null)
    {
        public bool HasUsableText => ExtractedTextLength > 0;

        public static OcrTextResult Success(
            string provider,
            string text,
            int pageCount,
            int pagesProcessed,
            bool fallbackUsed = false,
            string? fallbackReason = null,
            string? warning = null)
        {
            return new OcrTextResult(
                provider,
                text,
                pageCount,
                pagesProcessed,
                CountMeaningfulCharacters(text),
                fallbackUsed,
                fallbackReason,
                warning);
        }

        public static OcrTextResult Failure(
            string provider,
            string errorMessage,
            int pageCount = 0,
            int pagesProcessed = 0,
            string? fallbackReason = null)
        {
            return new OcrTextResult(
                provider,
                string.Empty,
                pageCount,
                pagesProcessed,
                0,
                provider.Equals("google-vision", StringComparison.OrdinalIgnoreCase),
                fallbackReason,
                ErrorMessage: errorMessage);
        }

        public static int CountMeaningfulCharacters(string text) =>
            string.IsNullOrWhiteSpace(text)
                ? 0
                : text.Count(c => !char.IsWhiteSpace(c));
    }
}
