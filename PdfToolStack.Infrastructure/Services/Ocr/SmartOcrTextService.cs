using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PdfToolStack.Infrastructure.Processors;

namespace PdfToolStack.Infrastructure.Services.Ocr
{
    public sealed class SmartOcrTextService
    {
        private readonly TesseractOcrTextProvider _tesseract;
        private readonly GoogleVisionOcrTextProvider _googleVision;
        private readonly GoogleVisionOptions _options;
        private readonly ILogger<SmartOcrTextService> _logger;

        public SmartOcrTextService(
            TesseractOcrTextProvider tesseract,
            GoogleVisionOcrTextProvider googleVision,
            IOptions<GoogleVisionOptions> options,
            ILogger<SmartOcrTextService> logger)
        {
            _tesseract = tesseract;
            _googleVision = googleVision;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<OcrTextResult> ExtractTextAsync(
            byte[] pdfBytes,
            OcrRequestContext context,
            CancellationToken cancellationToken = default)
        {
            var started = DateTimeOffset.UtcNow;
            var fallbackReason = string.Empty;
            OcrTextResult tesseractResult;

            try
            {
                tesseractResult = await _tesseract.ExtractTextAsync(
                    pdfBytes,
                    context,
                    cancellationToken: cancellationToken);
            }
            catch (OcrProcessingException ex)
            {
                fallbackReason = $"tesseract-exception:{ex.Message}";
                tesseractResult = OcrTextResult.Failure(
                    "tesseract",
                    ex.Message,
                    fallbackReason: fallbackReason);
            }

            if (context.HighAccuracy)
            {
                fallbackReason = "high-accuracy-requested";
            }
            else if (!string.IsNullOrWhiteSpace(tesseractResult.ErrorMessage))
            {
                fallbackReason = $"tesseract-error:{tesseractResult.ErrorMessage}";
            }
            else if (ShouldAcceptTesseract(tesseractResult))
            {
                _logger.LogInformation(
                    "Smart OCR selected Tesseract. Provider: {Provider}, PageCount: {PageCount}, PagesProcessed: {PagesProcessed}, ExtractedTextLength: {ExtractedTextLength}, UserId: {UserId}, IsPro: {IsPro}, ElapsedMs: {ElapsedMs}",
                    tesseractResult.Provider,
                    tesseractResult.PageCount,
                    tesseractResult.PagesProcessed,
                    tesseractResult.ExtractedTextLength,
                    context.UserId,
                    context.IsProUser,
                    (DateTimeOffset.UtcNow - started).TotalMilliseconds);

                return tesseractResult;
            }
            else
            {
                fallbackReason =
                    $"tesseract-low-quality:{tesseractResult.ExtractedTextLength}";
            }

            if (!_options.Enabled)
            {
                _logger.LogInformation(
                    "Google Vision OCR fallback skipped because disabled. FallbackReason: {FallbackReason}, UserId: {UserId}, IsPro: {IsPro}",
                    fallbackReason,
                    context.UserId,
                    context.IsProUser);

                return tesseractResult with { FallbackReason = fallbackReason };
            }

            if (string.Equals(
                context.UserId,
                "anonymous",
                StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Google Vision OCR fallback skipped for anonymous user. FallbackReason: {FallbackReason}",
                    fallbackReason);

                return tesseractResult with { FallbackReason = fallbackReason };
            }

            var pageLimit = context.IsProUser
                ? _options.MaxFallbackPagesForProUsers
                : _options.MaxFallbackPagesForFreeUsers;

            _logger.LogInformation(
                "Google Vision OCR fallback selected. FallbackReason: {FallbackReason}, MaxPages: {MaxPages}, UserId: {UserId}, IsPro: {IsPro}",
                fallbackReason,
                pageLimit,
                context.UserId,
                context.IsProUser);

            var googleResult = await _googleVision.ExtractTextAsync(
                pdfBytes,
                context,
                pageLimit,
                cancellationToken);

            if (googleResult.HasUsableText)
                return googleResult with { FallbackReason = fallbackReason };

            if (tesseractResult.HasUsableText)
                return tesseractResult with
                {
                    FallbackReason = fallbackReason,
                    Warning = googleResult.ErrorMessage
                };

            return googleResult with { FallbackReason = fallbackReason };
        }

        private bool ShouldAcceptTesseract(OcrTextResult result)
        {
            if (!result.HasUsableText)
                return false;

            if (result.PageCount <= 1)
                return true;

            return result.ExtractedTextLength >=
                _options.MinTesseractCharsBeforeFallback;
        }
    }
}
