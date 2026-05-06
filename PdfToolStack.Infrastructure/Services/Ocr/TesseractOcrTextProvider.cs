using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PdfToolStack.Infrastructure.Processors;

namespace PdfToolStack.Infrastructure.Services.Ocr
{
    public sealed class TesseractOcrTextProvider : IOcrTextProvider
    {
        private readonly PdfOcrProcessor _processor;
        private readonly ILogger<TesseractOcrTextProvider> _logger;

        public TesseractOcrTextProvider(
            PdfOcrProcessor processor,
            ILogger<TesseractOcrTextProvider> logger)
        {
            _processor = processor;
            _logger = logger;
        }

        public string ProviderName => "tesseract";

        public async Task<OcrTextProviderResult> ExtractTextAsync(
            OcrTextRequest request,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await _processor.ExtractTextWithInfoAsync(
                request.PdfBytes,
                request.Language,
                cancellationToken,
                request.MaxPages);

            var pageCount = request.TotalPageCount ?? result.PageCount;

            _logger.LogInformation(
                "OCR provider used: {Provider}. PageCount: {PageCount}, PagesProcessed: {PagesProcessed}, ExtractedTextLength: {ExtractedTextLength}, IsAnonymous: {IsAnonymous}, IsPro: {IsPro}, ElapsedMs: {ElapsedMs}",
                ProviderName,
                pageCount,
                result.PageCount,
                result.ExtractedTextLength,
                request.IsAnonymous,
                request.IsProUser,
                stopwatch.ElapsedMilliseconds);

            return new OcrTextProviderResult
            {
                Provider = ProviderName,
                Text = result.Text,
                PageCount = pageCount,
                PagesProcessed = result.PageCount,
                ExtractedTextLength = result.ExtractedTextLength,
                AverageConfidence = result.AverageConfidence
            };
        }
    }
}
