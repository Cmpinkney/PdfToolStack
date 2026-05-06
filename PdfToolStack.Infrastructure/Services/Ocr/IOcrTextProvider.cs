namespace PdfToolStack.Infrastructure.Services.Ocr
{
    public interface IOcrTextProvider
    {
        string ProviderName { get; }

        Task<OcrTextProviderResult> ExtractTextAsync(
            OcrTextRequest request,
            CancellationToken cancellationToken = default);
    }
}
