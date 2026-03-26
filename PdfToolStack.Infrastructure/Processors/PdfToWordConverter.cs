using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;

namespace PdfToolStack.Infrastructure.Processors
{
    public class PdfToWordConverter : IPdfProcessor
    {
        public ToolType ToolType => ToolType.PdfToWord;

        public async Task<byte[]> ProcessAsync(
            byte[] fileBytes,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var pages = ExtractTextFromPdf(fileBytes);
                return CreateWordDocument(pages);
            }, cancellationToken);
        }

        private List<string> ExtractTextFromPdf(byte[] fileBytes)
        {
            var pages = new List<string>();

            using var doc = PdfDocument.Open(fileBytes);

            foreach (var page in doc.GetPages())
            {
                var words = page.GetWords();
                var text = string.Join(" ",
                    words.Select(w => w.Text));
                pages.Add(text);
            }

            return pages;
        }

        private byte[] CreateWordDocument(List<string> pages)
        {
            using var outputStream = new MemoryStream();

            using var wordDoc = WordprocessingDocument.Create(
                outputStream,
                WordprocessingDocumentType.Document);

            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            AddStyles(mainPart);

            for (int i = 0; i < pages.Count; i++)
            {
                var pageText = pages[i];
                var sentences = pageText.Split('.',
                    StringSplitOptions.RemoveEmptyEntries);

                foreach (var sentence in sentences)
                {
                    var trimmed = sentence.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                        continue;

                    body.AppendChild(
                        CreateParagraph(trimmed + ".", false));
                }

                if (i < pages.Count - 1)
                    body.AppendChild(CreatePageBreak());
            }

            body.AppendChild(new SectionProperties());
            mainPart.Document.Save();

            return outputStream.ToArray();
        }

        private Paragraph CreateParagraph(
            string text, bool isHeading)
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

            runProps.AppendChild(
                new FontSize { Val = "22" });

            run.AppendChild(runProps);
            run.AppendChild(new Text(text)
            {
                Space = SpaceProcessingModeValues.Preserve
            });

            para.AppendChild(run);
            return para;
        }

        private Paragraph CreatePageBreak()
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

        private void AddStyles(MainDocumentPart mainPart)
        {
            var stylesPart = mainPart
                .AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = new Styles();

            var heading1 = new Style
            {
                Type = StyleValues.Paragraph,
                StyleId = "Heading1"
            };

            heading1.AppendChild(
                new StyleName { Val = "heading 1" });

            var rPr = new StyleRunProperties();
            rPr.AppendChild(new Bold());
            rPr.AppendChild(new FontSize { Val = "32" });
            rPr.AppendChild(new Color { Val = "2563EB" });
            heading1.AppendChild(rPr);

            stylesPart.Styles.AppendChild(heading1);
            stylesPart.Styles.Save();
        }
    }
}
