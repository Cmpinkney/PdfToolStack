using Google.Api.Gax.Grpc;
using Google.Cloud.Vision.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace PdfToolStack.Infrastructure.Services.Ocr
{
    public sealed class GoogleVisionOcrTextProvider : IOcrTextProvider
    {
        private readonly GoogleVisionOptions _options;
        private readonly ILogger<GoogleVisionOcrTextProvider> _logger;

        public GoogleVisionOcrTextProvider(
            IOptions<GoogleVisionOptions> options,
            ILogger<GoogleVisionOcrTextProvider> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public string ProviderName => "google-vision";

        public async Task<OcrTextResult> ExtractTextAsync(
            byte[] pdfBytes,
            OcrRequestContext context,
            int? maxPages = null,
            CancellationToken cancellationToken = default)
        {
            var started = DateTimeOffset.UtcNow;

            if (!_options.Enabled)
            {
                return OcrTextResult.Failure(
                    ProviderName,
                    "Google Vision OCR is disabled.");
            }

            try
            {
                var pageCount = PdfPageImageRenderer.CountPages(pdfBytes);
                var configuredLimit = context.IsProUser
                    ? _options.MaxFallbackPagesForProUsers
                    : _options.MaxFallbackPagesForFreeUsers;
                var pageLimit = maxPages.HasValue
                    ? Math.Min(maxPages.Value, configuredLimit)
                    : configuredLimit;
                var pagesToProcess = Math.Clamp(pageLimit, 1, pageCount);

                _logger.LogInformation(
                    "Google Vision OCR fallback starting. PageCount: {PageCount}, PagesProcessed: {PagesProcessed}, EstimatedVisionUnits: {EstimatedVisionUnits}, UserId: {UserId}, IsPro: {IsPro}",
                    pageCount,
                    pagesToProcess,
                    pagesToProcess,
                    context.UserId,
                    context.IsProUser);

                var pages = await PdfPageImageRenderer.RenderPagesAsync(
                    pdfBytes,
                    pagesToProcess,
                    cancellationToken);

                var builder = new ImageAnnotatorClientBuilder();
                if (!string.IsNullOrWhiteSpace(_options.CredentialsPath))
                    builder.CredentialsPath = _options.CredentialsPath;

                var client = await builder.BuildAsync(cancellationToken);
                var sb = new StringBuilder();

                foreach (var page in pages)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var image = Image.FromBytes(page.Bytes);
                    var annotation = await client.DetectDocumentTextAsync(
                        image,
                        imageContext: null,
                        callSettings: CallSettings.FromCancellationToken(
                            cancellationToken));

                    if (!string.IsNullOrWhiteSpace(annotation?.Text))
                    {
                        sb.AppendLine($"--- Page {page.PageNumber} ---");
                        sb.AppendLine(annotation.Text.Trim());
                        sb.AppendLine();
                    }
                }

                var text = sb.ToString().Trim();
                var warning = pageCount > pages.Count && !context.IsProUser
                    ? $"This review was generated from OCR text from the first {pages.Count} pages. Handwriting, poor scans, and unprocessed pages may reduce accuracy."
                    : "This review was generated from OCR text. Handwriting and poor scans may reduce accuracy.";
                var result = OcrTextResult.Success(
                    ProviderName,
                    text,
                    pageCount,
                    pages.Count,
                    fallbackUsed: true,
                    warning: warning);

                _logger.LogInformation(
                    "OCR provider used: {Provider}. PageCount: {PageCount}, PagesProcessed: {PagesProcessed}, ExtractedTextLength: {ExtractedTextLength}, UserId: {UserId}, IsPro: {IsPro}, ElapsedMs: {ElapsedMs}",
                    ProviderName,
                    result.PageCount,
                    result.PagesProcessed,
                    result.ExtractedTextLength,
                    context.UserId,
                    context.IsProUser,
                    (DateTimeOffset.UtcNow - started).TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Google Vision OCR fallback failed. UserId: {UserId}, IsPro: {IsPro}, ElapsedMs: {ElapsedMs}",
                    context.UserId,
                    context.IsProUser,
                    (DateTimeOffset.UtcNow - started).TotalMilliseconds);

                return OcrTextResult.Failure(
                    ProviderName,
                    "Google Vision OCR could not read this document.");
            }
        }
    }
}
