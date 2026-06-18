using Microsoft.Extensions.Logging;
using PdfToolStack.Infrastructure.Processors;

namespace PdfToolStack.Infrastructure.Services.Ocr
{
    public sealed class TesseractOcrTextProvider : IOcrTextProvider
    {
        private readonly string _tessDataPath;
        private readonly ILogger<TesseractOcrTextProvider> _logger;

        public TesseractOcrTextProvider(
            string tessDataPath,
            ILogger<TesseractOcrTextProvider> logger)
        {
            _tessDataPath = tessDataPath;
            _logger = logger;
        }

        public string ProviderName => "tesseract";

        public async Task<OcrTextResult> ExtractTextAsync(
            byte[] pdfBytes,
            OcrRequestContext context,
            int? maxPages = null,
            CancellationToken cancellationToken = default)
        {
            var started = DateTimeOffset.UtcNow;

            try
            {
                var pageCount = PdfPageImageRenderer.CountPages(pdfBytes);
                var pagesProcessed = maxPages.HasValue
                    ? Math.Min(maxPages.Value, pageCount)
                    : pageCount;

                var text = await Task.Run(
                    () =>
                    {
                        var processor = new PdfOcrProcessor(_tessDataPath);
                        return processor.ExtractText(
                            pdfBytes,
                            "eng",
                            cancellationToken,
                            maxPages);
                    },
                    cancellationToken);

                var result = OcrTextResult.Success(
                    ProviderName,
                    text,
                    pageCount,
                    pagesProcessed);

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
            catch (OcrProcessingException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Tesseract OCR text extraction failed. UserId: {UserId}, IsPro: {IsPro}, ElapsedMs: {ElapsedMs}",
                    context.UserId,
                    context.IsProUser,
                    (DateTimeOffset.UtcNow - started).TotalMilliseconds);

                return OcrTextResult.Failure(ProviderName, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Tesseract OCR text extraction errored. UserId: {UserId}, IsPro: {IsPro}, ElapsedMs: {ElapsedMs}",
                    context.UserId,
                    context.IsProUser,
                    (DateTimeOffset.UtcNow - started).TotalMilliseconds);

                return OcrTextResult.Failure(
                    ProviderName,
                    "Tesseract OCR could not read this document.");
            }
        }
    }
}
