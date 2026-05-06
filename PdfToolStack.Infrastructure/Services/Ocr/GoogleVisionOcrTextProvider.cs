using System.Diagnostics;
using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Api.Gax.Grpc;
using Google.Cloud.Vision.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PdfToolStack.Infrastructure.Configuration;
using PdfToolStack.Infrastructure.Processors;

namespace PdfToolStack.Infrastructure.Services.Ocr
{
    public sealed class GoogleVisionOcrTextProvider : IOcrTextProvider
    {
        private static readonly TimeSpan PageTimeout = TimeSpan.FromSeconds(30);

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

        public bool CanUse(out string reason)
        {
            if (!_options.Enabled)
            {
                reason = "google-vision-disabled";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_options.CredentialsPath))
            {
                if (File.Exists(_options.CredentialsPath))
                {
                    reason = "configured";
                    return true;
                }

                reason = "google-vision-credentials-path-missing";
                return false;
            }

            var envCredentials = Environment.GetEnvironmentVariable(
                "GOOGLE_APPLICATION_CREDENTIALS");

            if (!string.IsNullOrWhiteSpace(envCredentials))
            {
                reason = "configured";
                return true;
            }

            reason = "google-vision-credentials-not-configured";
            return false;
        }

        public async Task<OcrTextProviderResult> ExtractTextAsync(
            OcrTextRequest request,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var renderedPages = PdfOcrProcessor.RenderPages(
                request.PdfBytes,
                cancellationToken,
                request.MaxPages);

            if (renderedPages.Count == 0)
                return new OcrTextProviderResult
                {
                    Provider = ProviderName,
                    PageCount = request.TotalPageCount ?? 0
                };

            var client = await CreateClientAsync(cancellationToken);
            var sb = new StringBuilder();
            var blocks = new List<OcrTextBlock>();
            var confidences = new List<float>();

            for (var i = 0; i < renderedPages.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var image = Image.FromBytes(renderedPages[i].Bytes);
                using var pageCts =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);
                pageCts.CancelAfter(PageTimeout);

                var callSettings = CallSettings
                    .FromCancellationToken(pageCts.Token)
                    .WithTimeout(PageTimeout);

                var annotation = await client.DetectDocumentTextAsync(
                    image,
                    null,
                    callSettings);

                if (!string.IsNullOrWhiteSpace(annotation?.Text))
                {
                    sb.AppendLine(annotation.Text.Trim());
                    sb.AppendLine();
                }

                AddBlocks(annotation, i + 1, blocks, confidences);
            }

            var text = sb.ToString().Trim();
            var extractedTextLength =
                OcrTextQuality.CountMeaningfulCharacters(text);
            var pageCount = request.TotalPageCount ?? renderedPages.Count;

            _logger.LogInformation(
                "OCR provider used: {Provider}. PageCount: {PageCount}, PagesProcessed: {PagesProcessed}, ExtractedTextLength: {ExtractedTextLength}, EstimatedVisionUnits: {EstimatedVisionUnits}, IsAnonymous: {IsAnonymous}, IsPro: {IsPro}, ElapsedMs: {ElapsedMs}",
                ProviderName,
                pageCount,
                renderedPages.Count,
                extractedTextLength,
                renderedPages.Count,
                request.IsAnonymous,
                request.IsProUser,
                stopwatch.ElapsedMilliseconds);

            return new OcrTextProviderResult
            {
                Provider = ProviderName,
                Text = text,
                PageCount = pageCount,
                PagesProcessed = renderedPages.Count,
                ExtractedTextLength = extractedTextLength,
                AverageConfidence = confidences.Count > 0
                    ? confidences.Average()
                    : null,
                Blocks = blocks
            };
        }

        private async Task<ImageAnnotatorClient> CreateClientAsync(
            CancellationToken cancellationToken)
        {
            var builder = new ImageAnnotatorClientBuilder();

            if (!string.IsNullOrWhiteSpace(_options.CredentialsPath))
            {
                var credential =
                    await CredentialFactory.FromFileAsync<ServiceAccountCredential>(
                        _options.CredentialsPath,
                        cancellationToken);

                builder.GoogleCredential = credential.ToGoogleCredential();
            }

            if (!string.IsNullOrWhiteSpace(_options.ProjectId))
                builder.QuotaProject = _options.ProjectId;

            return await builder.BuildAsync(cancellationToken);
        }

        private static void AddBlocks(
            TextAnnotation? annotation,
            int pageNumber,
            List<OcrTextBlock> blocks,
            List<float> confidences)
        {
            if (annotation == null)
                return;

            foreach (var page in annotation.Pages)
            {
                foreach (var block in page.Blocks)
                {
                    if (block.Confidence > 0)
                        confidences.Add(block.Confidence);

                    var blockText = ExtractBlockText(block);
                    if (!string.IsNullOrWhiteSpace(blockText))
                    {
                        blocks.Add(new OcrTextBlock(
                            pageNumber,
                            blockText,
                            block.Confidence > 0
                                ? block.Confidence
                                : null));
                    }
                }
            }
        }

        private static string ExtractBlockText(Block block)
        {
            var paragraphs = new List<string>();

            foreach (var paragraph in block.Paragraphs)
            {
                var words = paragraph.Words
                    .Select(word => string.Concat(
                        word.Symbols.Select(symbol => symbol.Text)))
                    .Where(word => !string.IsNullOrWhiteSpace(word));

                var text = string.Join(" ", words).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    paragraphs.Add(text);
            }

            return string.Join(Environment.NewLine, paragraphs);
        }
    }
}
