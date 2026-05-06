using Docnet.Core;
using Docnet.Core.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using Tesseract;
using SystemDrawing = System.Drawing;
using SystemImaging = System.Drawing.Imaging;

namespace PdfToolStack.Infrastructure.Processors
{
    public sealed record PdfOcrPageImage(
        byte[] Bytes,
        int Width,
        int Height);

    public sealed record PdfOcrResult(
        byte[] PdfBytes,
        int PageCount,
        string Language,
        int ExtractedTextLength)
    {
        public bool HasExtractedText => ExtractedTextLength > 0;
    }

    public sealed record PdfOcrTextResult(
        string Text,
        int PageCount,
        string Language,
        int ExtractedTextLength,
        float? AverageConfidence)
    {
        public bool HasExtractedText => ExtractedTextLength > 0;
    }

    public sealed class OcrLanguageUnavailableException : Exception
    {
        public OcrLanguageUnavailableException(string message)
            : base(message)
        {
        }
    }

    public sealed class OcrProcessingException : Exception
    {
        public OcrProcessingException(string message)
            : base(message)
        {
        }
    }

    public class PdfOcrProcessor
    {
        private readonly string _tessDataPath;

        public PdfOcrProcessor(string tessDataPath)
        {
            _tessDataPath = tessDataPath;
        }

        public async Task<byte[]> ProcessAsync(
            byte[] pdfBytes,
            string language = "eng",
            CancellationToken cancellationToken = default,
            int? maxPages = null)
        {
            var result = await ProcessWithInfoAsync(
                pdfBytes, language, cancellationToken, maxPages);

            return result.PdfBytes;
        }

        public async Task<PdfOcrResult> ProcessWithInfoAsync(
            byte[] pdfBytes,
            string language = "eng",
            CancellationToken cancellationToken = default,
            int? maxPages = null)
        {
            language = ValidateLanguageData(language);

            return await Task.Run(() =>
            {
                var images = RenderPages(pdfBytes, cancellationToken, maxPages);

                if (images.Count == 0)
                    throw new OcrProcessingException(
                        "OCR could not render any pages from this PDF.");

                using var engine = new TesseractEngine(
                    _tessDataPath, language, EngineMode.Default);

                using var outputStream = new MemoryStream();
                using var doc = new Document();
                var writer = PdfWriter.GetInstance(doc, outputStream);
                doc.Open();

                var baseFont = BaseFont.CreateFont(
                    BaseFont.HELVETICA,
                    BaseFont.WINANSI,
                    BaseFont.NOT_EMBEDDED);
                var extractedTextLength = 0;

                foreach (var (imageBytes, width, height) in images)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var pix = Pix.LoadFromMemory(imageBytes);
                    using var page = engine.Process(pix);
                    var pageText = page.GetText() ?? string.Empty;
                    extractedTextLength += CountMeaningfulCharacters(pageText);

                    doc.SetPageSize(new iTextSharp.text.Rectangle(width, height));
                    doc.NewPage();

                    var img = iTextSharp.text.Image
                        .GetInstance(imageBytes);
                    img.SetAbsolutePosition(0, 0);
                    img.ScaleToFit(width, height);
                    doc.Add(img);

                    AddInvisibleTextLayer(
                        writer.DirectContent,
                        page,
                        pageText,
                        baseFont,
                        width,
                        height);
                }

                if (extractedTextLength == 0)
                    throw new OcrProcessingException(
                        "OCR did not detect any text in this PDF. Please try a clearer scan.");

                doc.Close();
                var outputBytes = outputStream.ToArray();

                if (outputBytes.Length == 0)
                    throw new OcrProcessingException(
                        "OCR generated an empty PDF. Please try again.");

                return new PdfOcrResult(
                    outputBytes,
                    images.Count,
                    language,
                    extractedTextLength);

            }, cancellationToken);
        }

        public string ExtractText(
            byte[] pdfBytes,
            string language = "eng",
            CancellationToken cancellationToken = default)
        {
            language = ValidateLanguageData(language);

            var images = RenderPages(pdfBytes, cancellationToken);
            var sb = new System.Text.StringBuilder();

            using var engine = new TesseractEngine(
                _tessDataPath, language, EngineMode.Default);

            foreach (var (imageBytes, _, _) in images)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var pix = Pix.LoadFromMemory(imageBytes);
                using var page = engine.Process(pix);
                sb.AppendLine(page.GetText());
            }

            return sb.ToString();
        }

        public async Task<PdfOcrTextResult> ExtractTextWithInfoAsync(
            byte[] pdfBytes,
            string language = "eng",
            CancellationToken cancellationToken = default,
            int? maxPages = null)
        {
            language = ValidateLanguageData(language);

            return await Task.Run(() =>
            {
                var images = RenderPages(pdfBytes, cancellationToken, maxPages);

                if (images.Count == 0)
                    throw new OcrProcessingException(
                        "OCR could not render any pages from this PDF.");

                var sb = new System.Text.StringBuilder();
                var extractedTextLength = 0;
                var confidences = new List<float>();

                using var engine = new TesseractEngine(
                    _tessDataPath, language, EngineMode.Default);

                foreach (var (imageBytes, _, _) in images)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var pix = Pix.LoadFromMemory(imageBytes);
                    using var page = engine.Process(pix);
                    var pageText = page.GetText() ?? string.Empty;
                    extractedTextLength += CountMeaningfulCharacters(pageText);
                    sb.AppendLine(pageText);

                    var confidence = page.GetMeanConfidence();
                    if (confidence > 0)
                        confidences.Add(confidence);
                }

                if (extractedTextLength == 0)
                    throw new OcrProcessingException(
                        "OCR did not detect any text in this PDF. Please try a clearer scan.");

                return new PdfOcrTextResult(
                    sb.ToString().Trim(),
                    images.Count,
                    language,
                    extractedTextLength,
                    confidences.Count > 0
                        ? confidences.Average()
                        : null);

            }, cancellationToken);
        }

        private string ValidateLanguageData(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
                throw new ArgumentException(
                    "Please choose a valid OCR language.",
                    nameof(language));

            var languages = language
                .Split('+')
                .Select(part => part.Trim())
                .ToArray();

            if (languages.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException(
                    "Please choose a valid OCR language.",
                    nameof(language));

            foreach (var item in languages)
            {
                if (item.Contains('/') ||
                    item.Contains('\\') ||
                    item.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new ArgumentException(
                        "Please choose a valid OCR language.",
                        nameof(language));
                }

                var trainedDataPath = Path.Combine(
                    _tessDataPath,
                    $"{item}.traineddata");

                if (!File.Exists(trainedDataPath))
                {
                    throw new OcrLanguageUnavailableException(
                        "OCR language is not available yet. Please choose English.");
                }
            }

            return string.Join('+', languages);
        }

        private static void AddInvisibleTextLayer(
            PdfContentByte canvas,
            Page page,
            string fallbackText,
            BaseFont font,
            int pageWidth,
            int pageHeight)
        {
            // Word-level placement must run first.
            // This gives browser/Acrobat search one text object per visible word,
            // which improves highlighting, selection, and copy/paste.
            if (TryAddLineTextLayer(canvas, page, font, pageWidth, pageHeight))
                return;

            if (TryAddWordTextLayer(canvas, page, font, pageWidth, pageHeight))
                return;

            AddFallbackInvisibleLines(canvas, fallbackText, font, pageHeight);
        }


        private static bool TryAddLineTextLayer(
    PdfContentByte canvas,
    Page page,
    BaseFont font,
    int pageWidth,
    int pageHeight)
        {
            var hasPositionedText = false;

            using var iterator = page.GetIterator();
            iterator.Begin();

            do
            {
                var text = NormalizeOcrLine(iterator.GetText(PageIteratorLevel.TextLine));

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (!iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out var bounds))
                    continue;

                if (!IsUsableBounds(bounds, pageWidth, pageHeight))
                    continue;

                var x = ClampToPage(bounds.X1, pageWidth);
                var y = ClampToPage(pageHeight - bounds.Y2, pageHeight);

                var boxWidth = Math.Max(1f, bounds.Width);
                var boxHeight = Math.Max(1f, bounds.Height);
                var fontSize = Math.Clamp(boxHeight * 0.54f, 5f, 16f);

                var textWidth = font.GetWidthPoint(text, fontSize);
                var horizontalScale = 100f;

                if (textWidth > 0)
                {
                    var desiredScale = (boxWidth / textWidth) * 100f;
                    horizontalScale = Math.Clamp(desiredScale, 95f, 135f);
                }

                WriteInvisibleText(canvas, font, text, x, y, fontSize, horizontalScale);
                hasPositionedText = true;
            }
            while (iterator.Next(PageIteratorLevel.TextLine));

            return hasPositionedText;
        }

        private static bool TryAddWordTextLayer(
            PdfContentByte canvas,
            Page page,
            BaseFont font,
            int pageWidth,
            int pageHeight)
        {
            var hasPositionedText = false;

            using var iterator = page.GetIterator();
            iterator.Begin();

            do
            {
                var rawText = iterator.GetText(PageIteratorLevel.Word);
                var word = NormalizeOcrToken(rawText);

                if (string.IsNullOrWhiteSpace(word))
                    continue;

                if (!iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var bounds))
                    continue;

                if (!IsUsableBounds(bounds, pageWidth, pageHeight))
                    continue;

                var x = ClampToPage(bounds.X1, pageWidth);
                var y = ClampToPage(pageHeight - bounds.Y2, pageHeight);

                var boxWidth = Math.Max(1f, bounds.Width);
                var boxHeight = Math.Max(1f, bounds.Height);

                // Keep font slightly smaller than the OCR box so the invisible text
                // does not bleed into neighboring words and cause sticky highlights.
                var fontSize = Math.Clamp(boxHeight * 0.72f, 5f, 18f);

                // Avoid aggressive horizontal scaling. Over-compression is a common
                // reason Chrome/Edge copy-paste drops letters from invisible OCR text.
                // Do not aggressively scale word text.
                // Browser PDF selection highlight is based on glyph geometry.
                // Tight scaling causes partial-character highlight gaps even when copy/paste works.
                var horizontalScale = 100f;

                // Add a trailing space as its own text content so selected words copy cleanly.
                WriteInvisibleText(canvas, font, word + " ", x, y, fontSize, horizontalScale);
                hasPositionedText = true;
            }
            while (iterator.Next(PageIteratorLevel.Word));

            return hasPositionedText;
        }

        private static void AddFallbackInvisibleLines(
    PdfContentByte canvas,
    string text,
    BaseFont font,
    int pageHeight)
        {
            var lines = text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => NormalizeOcrLine(line))
                .Where(line => line.Length > 0)
                .ToArray();

            if (lines.Length == 0)
                return;

            var y = pageHeight - 36f;

            foreach (var line in lines)
            {
                if (y < 24f)
                    break;

                WriteInvisibleText(canvas, font, line, 36f, y, 10f, 100f);
                y -= 14f;
            }
        }

        private static void WriteInvisibleText(
    PdfContentByte canvas,
    BaseFont font,
    string text,
    float x,
    float y,
    float fontSize,
    float horizontalScale)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            canvas.SaveState();
            canvas.BeginText();
            canvas.SetTextRenderingMode(PdfContentByte.TEXT_RENDER_MODE_INVISIBLE);
            canvas.SetFontAndSize(font, fontSize);
            canvas.SetHorizontalScaling(horizontalScale);
            canvas.SetTextMatrix(x, y);
            canvas.ShowText(text);
            canvas.EndText();
            canvas.RestoreState();
        }

        private static string NormalizeOcrToken(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return text
                .Replace("\r", "")
                .Replace("\n", "")
                .Trim();
        }

        private static bool IsUsableBounds(
            Tesseract.Rect bounds,
            int pageWidth,
            int pageHeight)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return false;

            if (bounds.X2 < 0 || bounds.Y2 < 0)
                return false;

            if (bounds.X1 > pageWidth || bounds.Y1 > pageHeight)
                return false;

            return true;
        }

        private static float ClampToPage(float value, int max) =>
            Math.Clamp(value, 0, Math.Max(0, max - 1));

        private static string NormalizeOcrLine(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return string.Join(
                " ",
                text.Replace("\r\n", "\n")
                    .Replace('\r', '\n')
                    .Split(
                        [' ', '\t', '\n'],
                        StringSplitOptions.RemoveEmptyEntries));
        }

        private static int CountMeaningfulCharacters(string text) =>
            text.Count(c => !char.IsWhiteSpace(c));

        public static List<PdfOcrPageImage>
            RenderPages(byte[] pdfBytes,
                CancellationToken cancellationToken,
                int? maxPages = null)
        {
            var results = new List<PdfOcrPageImage>();

            using var lib = DocLib.Instance;
            using var doc = lib.GetDocReader(
                pdfBytes, new PageDimensions(2000, 2800));

            var count = doc.GetPageCount();

            if (count == 0)
                throw new OcrProcessingException(
                    "OCR could not find any pages in this PDF.");

            var pagesToRender = maxPages.HasValue
                ? Math.Clamp(maxPages.Value, 1, count)
                : count;

            for (int i = 0; i < pagesToRender; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var pageReader = doc.GetPageReader(i);
                var w = pageReader.GetPageWidth();
                var h = pageReader.GetPageHeight();
                var raw = pageReader.GetImage();

                using var bmp = new SystemDrawing.Bitmap(w, h,
                SystemImaging.PixelFormat.Format32bppArgb);
                var data = bmp.LockBits(
                    new SystemDrawing.Rectangle(0, 0, w, h),
                    SystemImaging.ImageLockMode.WriteOnly,
                    SystemImaging.PixelFormat.Format32bppArgb);
                Marshal.Copy(raw, 0, data.Scan0, raw.Length);
                bmp.UnlockBits(data);

                using var ms = new MemoryStream();
                bmp.Save(ms, SystemImaging.ImageFormat.Png);
                results.Add(new PdfOcrPageImage(ms.ToArray(), w, h));
            }

            return results;
        }
    }
}
