namespace PdfToolStack.Infrastructure.Services.Ocr
{
    public interface IOcrTextProvider
    {
        string ProviderName { get; }

        Task<OcrTextResult> ExtractTextAsync(
            byte[] pdfBytes,
            OcrRequestContext context,
            int? maxPages = null,
            CancellationToken cancellationToken = default);
    }
}
