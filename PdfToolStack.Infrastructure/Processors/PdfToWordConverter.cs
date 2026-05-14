using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;
using System.Text;
using System.Xml;
using UglyToad.PdfPig;

namespace PdfToolStack.Infrastructure.Processors
{
    public class PdfToWordConverter : IPdfProcessor
    {
        private const double BlankPageThreshold = 0.40;
        private const int LowMeaningfulCharacterThreshold = 20;
        private const int LowWordCountThreshold = 4;
        private const string OcrRequiredMessage =
            "This PDF appears to contain scanned or image-only pages. " +
            "Run OCR first, then convert the searchable PDF to Word.";

        private readonly ILogger<PdfToWordConverter> _logger;

        public PdfToWordConverter(ILogger<PdfToWordConverter> logger)
        {
            _logger = logger;
        }

        public ToolType ToolType => ToolType.PdfToWord;

        public async Task<byte[]> ProcessAsync(
            byte[] fileBytes,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var pages = ExtractTextFromPdf(fileBytes);
                var docxBytes = CreateWordDocument(pages);
                _logger.LogDebug(
                    "[PdfToWordDiag] Converter generated DOCX bytes length={Size}",
                    docxBytes.Length);
                return docxBytes;
            }, cancellationToken);
        }

        private List<string> ExtractTextFromPdf(byte[] fileBytes)
        {
            var pages = new List<string>();

            using var doc = PdfDocument.Open(fileBytes);
            var totalPages = doc.NumberOfPages;
            var blankPages = 0;

            _logger.LogInformation(
                "PDF to Word page count: {PageCount}",
                totalPages);

            foreach (var page in doc.GetPages())
            {
                var words = page.GetWords();
                var raw = string.Join(" ", words.Select(w => w.Text));
                var text = SanitizeText(raw);
                var stats = AnalyzePageText(text);

                if (stats.IsEffectivelyBlank)
                    blankPages++;

                pages.Add(text);

                _logger.LogDebug(
                    "PDF to Word page {PageNumber}: extracted chars={ExtractedCharCount}, meaningful chars={MeaningfulCharCount}, words={WordCount}, treatedBlank={TreatedBlank}",
                    page.Number,
                    text.Length,
                    stats.MeaningfulCharacterCount,
                    stats.WordCount,
                    stats.IsEffectivelyBlank);
            }

            var totalChars = pages.Sum(p => p.Trim().Length);
            var blankRatio = totalPages > 0
                ? (double)blankPages / totalPages
                : 0;

            _logger.LogInformation(
                "PDF to Word extraction summary: pages={TotalPages}, blankPages={BlankPages}, blankRatio={BlankRatio:P0}, totalChars={TotalChars}",
                totalPages,
                blankPages,
                blankRatio,
                totalChars);

            if (blankRatio >= BlankPageThreshold)
            {
                _logger.LogWarning(
                    "PDF to Word OCR required: {BlankPages}/{TotalPages} pages blank or effectively blank ({BlankRatio:P0} >= {Threshold:P0}), {TotalChars} chars extracted",
                    blankPages,
                    totalPages,
                    blankRatio,
                    BlankPageThreshold,
                    totalChars);

                throw new InvalidOperationException(OcrRequiredMessage);
            }

            if (blankPages > 0)
            {
                _logger.LogWarning(
                    "PDF to Word continuing with partial blank pages: {BlankPages}/{TotalPages} pages blank or effectively blank ({BlankRatio:P0}); {TotalChars} chars extracted",
                    blankPages,
                    totalPages,
                    blankRatio,
                    totalChars);
            }

            return pages;
        }

        private static PageTextStats AnalyzePageText(string text)
        {
            var trimmedCharacterCount = text.Trim().Length;
            var meaningfulCharacterCount = text.Count(char.IsLetterOrDigit);
            var wordCount = CountWords(text);
            var isEffectivelyBlank =
                trimmedCharacterCount == 0 ||
                (meaningfulCharacterCount < LowMeaningfulCharacterThreshold &&
                 wordCount < LowWordCountThreshold);

            return new PageTextStats(
                trimmedCharacterCount,
                meaningfulCharacterCount,
                wordCount,
                isEffectivelyBlank);
        }

        private static int CountWords(string text)
        {
            var count = 0;
            var inWord = false;

            foreach (var c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (!inWord)
                    {
                        count++;
                        inWord = true;
                    }
                }
                else
                {
                    inWord = false;
                }
            }

            return count;
        }

        private static string SanitizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (XmlConvert.IsXmlChar(c))
                    sb.Append(c);
            }

            return sb.ToString();
        }

        private byte[] CreateWordDocument(List<string> pages)
        {
            using var outputStream = new MemoryStream();

            using (var wordDoc = WordprocessingDocument.Create(
                outputStream,
                WordprocessingDocumentType.Document))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                AddStyles(mainPart);

                for (var i = 0; i < pages.Count; i++)
                {
                    var pageText = pages[i].Trim();

                    if (!string.IsNullOrWhiteSpace(pageText))
                        body.AppendChild(CreateParagraph(pageText));

                    if (i < pages.Count - 1)
                        body.AppendChild(CreatePageBreak());
                }

                body.AppendChild(new SectionProperties());
                mainPart.Document.Save();
            }

            return outputStream.ToArray();
        }

        private static Paragraph CreateParagraph(string text)
        {
            var para = new Paragraph();
            var paraProps = new ParagraphProperties();

            paraProps.AppendChild(
                new SpacingBetweenLines
                {
                    After = "120",
                    Line = "276",
                    LineRule = LineSpacingRuleValues.Auto
                });

            para.AppendChild(paraProps);

            var run = new Run();
            var runProps = new RunProperties();

            runProps.AppendChild(new RunFonts
            {
                Ascii = "Calibri",
                HighAnsi = "Calibri"
            });

            runProps.AppendChild(new FontSize { Val = "22" });

            run.AppendChild(runProps);
            run.AppendChild(new Text(text)
            {
                Space = SpaceProcessingModeValues.Preserve
            });

            para.AppendChild(run);
            return para;
        }

        private static Paragraph CreatePageBreak()
        {
            var para = new Paragraph();
            var run = new Run();
            run.AppendChild(new Break
            {
                Type = BreakValues.Page
            });
            para.AppendChild(run);
            return para;
        }

        private static void AddStyles(MainDocumentPart mainPart)
        {
            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = new Styles();

            var heading1 = new Style
            {
                Type = StyleValues.Paragraph,
                StyleId = "Heading1"
            };

            heading1.AppendChild(new StyleName { Val = "heading 1" });

            var rPr = new StyleRunProperties();
            rPr.AppendChild(new Bold());
            rPr.AppendChild(new FontSize { Val = "32" });
            rPr.AppendChild(new Color { Val = "2563EB" });
            heading1.AppendChild(rPr);

            stylesPart.Styles.AppendChild(heading1);
            stylesPart.Styles.Save();
        }

        private sealed record PageTextStats(
            int TrimmedCharacterCount,
            int MeaningfulCharacterCount,
            int WordCount,
            bool IsEffectivelyBlank);
    }
}
