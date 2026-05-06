using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PdfToolStack.Infrastructure.Configuration;
using PdfToolStack.Infrastructure.Processors;

namespace PdfToolStack.Infrastructure.Services.Ocr
{
    public sealed class SmartOcrTextService
    {
        private const string ScannedLowQualityMessage =
            "This appears to be a scanned PDF and the text quality is too low for contract review. Try a clearer scan.";

        private readonly TesseractOcrTextProvider _tesseractProvider;
        private readonly GoogleVisionOcrTextProvider _googleVisionProvider;
        private readonly GoogleVisionOptions _options;
        private readonly ILogger<SmartOcrTextService> _logger;

        public SmartOcrTextService(
            TesseractOcrTextProvider tesseractProvider,
            GoogleVisionOcrTextProvider googleVisionProvider,
            IOptions<GoogleVisionOptions> options,
            ILogger<SmartOcrTextService> logger)
        {
            _tesseractProvider = tesseractProvider;
            _googleVisionProvider = googleVisionProvider;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<SmartOcrTextResult> ExtractTextAsync(
            OcrTextRequest request,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var maxAllowedPages = GetMaxFallbackPages(request.IsProUser);
            var googlePagesToProcess = GetPagesToProcess(
                request,
                maxAllowedPages);
            var totalPageCount = request.TotalPageCount ?? 0;
            var fallbackReason = string.Empty;
            OcrTextProviderResult? tesseractResult = null;

            try
            {
                tesseractResult = await _tesseractProvider.ExtractTextAsync(
                    request,
                    cancellationToken);
            }
            catch (OcrProcessingException ex)
            {
                fallbackReason = "tesseract-no-text";
                _logger.LogWarning(
                    ex,
                    "Tesseract OCR produced no usable text. PageCount: {PageCount}, PagesProcessed: {PagesProcessed}, IsAnonymous: {IsAnonymous}, IsPro: {IsPro}",
                    totalPageCount,
                    request.MaxPages ?? request.TotalPageCount ?? 0,
                    request.IsAnonymous,
                    request.IsProUser);
            }
            catch (Exception ex)
            {
                fallbackReason = "tesseract-error";
                _logger.LogWarning(
                    ex,
                    "Tesseract OCR failed before Google Vision fallback. PageCount: {PageCount}, PagesProcessed: {PagesProcessed}, IsAnonymous: {IsAnonymous}, IsPro: {IsPro}",
                    totalPageCount,
                    request.MaxPages ?? request.TotalPageCount ?? 0,
                    request.IsAnonymous,
                    request.IsProUser);
            }

            var tesseractNeedsFallback = NeedsGoogleFallback(
                request,
                tesseractResult,
                totalPageCount,
                ref fallbackReason);

            var tesseractIsPartial = tesseractResult != null &&
                IsPartial(tesseractResult.PageCount, tesseractResult.PagesProcessed);

            if (!tesseractNeedsFallback && tesseractResult != null)
            {
                return LogAndReturn(
                    SmartOcrTextResult.FromProvider(
                        tesseractResult,
                        wasGoogleVisionFallbackUsed: false,
                        fallbackReason: null,
                        tesseractIsPartial),
                    request,
                    stopwatch);
            }

            if (!request.AllowGoogleVisionFallback || request.IsAnonymous)
            {
                return LogAndReturn(
                    tesseractResult?.HasExtractedText == true
                        ? SmartOcrTextResult.FromProvider(
                            tesseractResult,
                            wasGoogleVisionFallbackUsed: false,
                            fallbackReason,
                            tesseractIsPartial)
                        : SmartOcrTextResult.Failure(
                            ScannedLowQualityMessage,
                            fallbackReason,
                            totalPageCount,
                            tesseractResult?.PagesProcessed ?? 0),
                    request,
                    stopwatch);
            }

            if (!_googleVisionProvider.CanUse(out var configReason))
            {
                _logger.LogInformation(
                    "Google Vision fallback skipped. Reason: {Reason}, PageCount: {PageCount}, PagesProcessed: {PagesProcessed}, IsAnonymous: {IsAnonymous}, IsPro: {IsPro}",
                    configReason,
                    totalPageCount,
                    googlePagesToProcess,
                    request.IsAnonymous,
                    request.IsProUser);

                return LogAndReturn(
                    tesseractResult?.HasExtractedText == true
                        ? SmartOcrTextResult.FromProvider(
                            tesseractResult,
                            wasGoogleVisionFallbackUsed: false,
                            fallbackReason,
                            tesseractIsPartial)
                        : SmartOcrTextResult.Failure(
                            ScannedLowQualityMessage,
                            configReason,
                            totalPageCount,
                            tesseractResult?.PagesProcessed ?? 0),
                    request,
                    stopwatch);
            }

            var googleWouldBePartial = IsPartial(
                totalPageCount,
                googlePagesToProcess);

            if (request.RequireCompleteDocument && googleWouldBePartial)
            {
                if (!request.IsProUser)
                {
                    return LogAndReturn(
                        SmartOcrTextResult.UpgradeRequired(
                            "Scanned contract review for documents over " +
                            $"{maxAllowedPages} pages requires Pro. " +
                            "Upgrade to review the full scanned contract.",
                            string.IsNullOrWhiteSpace(fallbackReason)
                                ? "free-page-cap"
                                : fallbackReason,
                            totalPageCount,
                            googlePagesToProcess),
                        request,
                        stopwatch);
                }

                return LogAndReturn(
                    SmartOcrTextResult.Failure(
                        "Scanned contract review can process up to " +
                        $"{maxAllowedPages} pages with OCR fallback. " +
                        "Try a shorter contract or upload a clearer text PDF.",
                        "pro-page-cap",
                        totalPageCount,
                        googlePagesToProcess),
                    request,
                    stopwatch);
            }

            try
            {
                _logger.LogInformation(
                    "Google Vision fallback starting. FallbackReason: {FallbackReason}, PageCount: {PageCount}, PagesProcessed: {PagesProcessed}, EstimatedVisionUnits: {EstimatedVisionUnits}, IsAnonymous: {IsAnonymous}, IsPro: {IsPro}",
                    fallbackReason,
                    totalPageCount,
                    googlePagesToProcess,
                    googlePagesToProcess,
                    request.IsAnonymous,
                    request.IsProUser);

                var googleResult =
                    await _googleVisionProvider.ExtractTextAsync(
                        request with { MaxPages = googlePagesToProcess },
                        cancellationToken);

                if (googleResult.HasExtractedText)
                {
                    return LogAndReturn(
                        SmartOcrTextResult.FromProvider(
                            googleResult,
                            wasGoogleVisionFallbackUsed: true,
                            fallbackReason,
                            googleWouldBePartial),
                        request,
                        stopwatch);
                }

                return LogAndReturn(
                    tesseractResult?.HasExtractedText == true
                        ? SmartOcrTextResult.FromProvider(
                            tesseractResult,
                            wasGoogleVisionFallbackUsed: false,
                            fallbackReason,
                            tesseractIsPartial)
                        : SmartOcrTextResult.Failure(
                            ScannedLowQualityMessage,
                            "google-vision-no-text",
                            totalPageCount,
                            googlePagesToProcess),
                    request,
                    stopwatch);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Google Vision fallback failed. FallbackReason: {FallbackReason}, PageCount: {PageCount}, PagesProcessed: {PagesProcessed}, IsAnonymous: {IsAnonymous}, IsPro: {IsPro}",
                    fallbackReason,
                    totalPageCount,
                    googlePagesToProcess,
                    request.IsAnonymous,
                    request.IsProUser);

                return LogAndReturn(
                    tesseractResult?.HasExtractedText == true
                        ? SmartOcrTextResult.FromProvider(
                            tesseractResult,
                            wasGoogleVisionFallbackUsed: false,
                            fallbackReason,
                            tesseractIsPartial)
                        : SmartOcrTextResult.Failure(
                            ScannedLowQualityMessage,
                            "google-vision-error",
                            totalPageCount,
                            googlePagesToProcess),
                    request,
                    stopwatch);
            }
        }

        private bool NeedsGoogleFallback(
            OcrTextRequest request,
            OcrTextProviderResult? tesseractResult,
            int totalPageCount,
            ref string fallbackReason)
        {
            if (request.HighAccuracy)
            {
                fallbackReason = "high-accuracy-requested";
                return true;
            }

            if (tesseractResult == null || !tesseractResult.HasExtractedText)
            {
                if (string.IsNullOrWhiteSpace(fallbackReason))
                    fallbackReason = "tesseract-no-text";

                return true;
            }

            var threshold = Math.Max(
                1,
                _options.MinTesseractCharsBeforeFallback);

            if (totalPageCount > 1 &&
                tesseractResult.ExtractedTextLength < threshold)
            {
                fallbackReason = "tesseract-low-text";
                return true;
            }

            return false;
        }

        private int GetMaxFallbackPages(bool isProUser)
        {
            var configured = isProUser
                ? _options.MaxFallbackPagesForProUsers
                : _options.MaxFallbackPagesForFreeUsers;

            return Math.Max(1, configured);
        }

        private static int GetPagesToProcess(
            OcrTextRequest request,
            int maxAllowedPages)
        {
            var pagesToProcess = request.MaxPages.HasValue
                ? Math.Min(request.MaxPages.Value, maxAllowedPages)
                : maxAllowedPages;

            if (request.TotalPageCount.HasValue)
                pagesToProcess = Math.Min(
                    pagesToProcess,
                    request.TotalPageCount.Value);

            return Math.Max(1, pagesToProcess);
        }

        private static bool IsPartial(int totalPageCount, int pagesProcessed) =>
            totalPageCount > 0 && pagesProcessed < totalPageCount;

        private SmartOcrTextResult LogAndReturn(
            SmartOcrTextResult result,
            OcrTextRequest request,
            Stopwatch stopwatch)
        {
            _logger.LogInformation(
                "Smart OCR completed. Provider: {Provider}, PageCount: {PageCount}, PagesProcessed: {PagesProcessed}, ExtractedTextLength: {ExtractedTextLength}, FallbackReason: {FallbackReason}, GoogleFallbackUsed: {GoogleFallbackUsed}, RequiresUpgrade: {RequiresUpgrade}, IsAnonymous: {IsAnonymous}, IsPro: {IsPro}, ElapsedMs: {ElapsedMs}",
                string.IsNullOrWhiteSpace(result.ProviderUsed)
                    ? "none"
                    : result.ProviderUsed,
                result.PageCount,
                result.PagesProcessed,
                result.ExtractedTextLength,
                result.FallbackReason,
                result.WasGoogleVisionFallbackUsed,
                result.RequiresUpgrade,
                request.IsAnonymous,
                request.IsProUser,
                stopwatch.ElapsedMilliseconds);

            return result;
        }
    }
}
