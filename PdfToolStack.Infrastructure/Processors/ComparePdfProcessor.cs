using iTextSharp.text;
using iTextSharp.text.pdf;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PdfToolStack.Infrastructure.Processors
{
    public class ComparePdfProcessor
    {
        public Task<CompareResult> CompareAsync(
            byte[] originalBytes,
            byte[] revisedBytes,
            CancellationToken cancellationToken = default)
        {
            var originalPages = ExtractPages(originalBytes);
            var revisedPages = ExtractPages(revisedBytes);

            var pageCount = Math.Max(originalPages.Count, revisedPages.Count);
            var diffPages = new List<PageDiff>();
            int totalAdded = 0, totalRemoved = 0, totalUnchanged = 0;

            for (int i = 0; i < pageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var originalWords = i < originalPages.Count
                    ? originalPages[i] : new List<string>();
                var revisedWords = i < revisedPages.Count
                    ? revisedPages[i] : new List<string>();

                var diff = DiffWords(originalWords, revisedWords);
                var added = diff.Count(d => d.Type == DiffType.Added);
                var removed = diff.Count(d => d.Type == DiffType.Removed);
                var unchanged = diff.Count(d => d.Type == DiffType.Unchanged);

                totalAdded += added;
                totalRemoved += removed;
                totalUnchanged += unchanged;

                diffPages.Add(new PageDiff
                {
                    PageNumber = i + 1,
                    Chunks = diff,
                    AddedWords = added,
                    RemovedWords = removed,
                    UnchangedWords = unchanged
                });
            }

            var reportBytes = BuildReport(diffPages, pageCount);

            return Task.FromResult(new CompareResult
            {
                ReportBytes = reportBytes,
                TotalPagesCompared = pageCount,
                TotalAddedWords = totalAdded,
                TotalRemovedWords = totalRemoved,
                TotalUnchangedWords = totalUnchanged,
                HasDifferences = totalAdded > 0 || totalRemoved > 0
            });
        }

        private static List<List<string>> ExtractPages(byte[] pdfBytes)
        {
            var pages = new List<List<string>>();
            try
            {
                using var doc = UglyToad.PdfPig.PdfDocument.Open(pdfBytes);
                foreach (var page in doc.GetPages())
                {
                    var words = page.GetWords()
                        .Select(w => w.Text)
                        .ToList();
                    pages.Add(words);
                }
            }
            catch
            {
                // Return empty if extraction fails
            }
            return pages;
        }

        private static List<DiffChunk> DiffWords(
            List<string> original,
            List<string> revised)
        {
            // Myers diff algorithm — LCS-based word diff
            int n = original.Count, m = revised.Count;
            var lcs = new int[n + 1, m + 1];

            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                    lcs[i, j] = original[i - 1] == revised[j - 1]
                        ? lcs[i - 1, j - 1] + 1
                        : Math.Max(lcs[i - 1, j], lcs[i, j - 1]);

            var chunks = new List<DiffChunk>();
            int oi = n, ri = m;

            while (oi > 0 || ri > 0)
            {
                if (oi > 0 && ri > 0 &&
                    original[oi - 1] == revised[ri - 1])
                {
                    chunks.Add(new DiffChunk(
                        original[oi - 1], DiffType.Unchanged));
                    oi--; ri--;
                }
                else if (ri > 0 && (oi == 0 ||
                    lcs[oi, ri - 1] >= lcs[oi - 1, ri]))
                {
                    chunks.Add(new DiffChunk(
                        revised[ri - 1], DiffType.Added));
                    ri--;
                }
                else
                {
                    chunks.Add(new DiffChunk(
                        original[oi - 1], DiffType.Removed));
                    oi--;
                }
            }

            chunks.Reverse();
            return chunks;
        }

        private static byte[] BuildReport(
            List<PageDiff> diffPages, int totalPages)
        {
            using var ms = new MemoryStream();
            using var doc = new Document(iTextSharp.text.PageSize.A4, 50, 50, 60, 60);
            var writer = PdfWriter.GetInstance(doc, ms);
            doc.Open();

            // Fonts
            var titleFont = FontFactory.GetFont(
                FontFactory.HELVETICA_BOLD, 18, new BaseColor(30, 30, 30));
            var headingFont = FontFactory.GetFont(
                FontFactory.HELVETICA_BOLD, 12, new BaseColor(50, 50, 50));
            var normalFont = FontFactory.GetFont(
                FontFactory.HELVETICA, 10, new BaseColor(60, 60, 60));
            var addedFont = FontFactory.GetFont(
                FontFactory.HELVETICA, 10, new BaseColor(22, 101, 52));
            var removedFont = FontFactory.GetFont(
                FontFactory.HELVETICA, 10, new BaseColor(185, 28, 28));

            // Colors
            var addedBg = new BaseColor(220, 252, 231);
            var removedBg = new BaseColor(254, 226, 226);
            var headerBg = new BaseColor(243, 244, 246);

            // Title
            doc.Add(new Paragraph("PDF Comparison Report", titleFont)
            {
                SpacingAfter = 4
            });
            doc.Add(new Paragraph(
                $"Generated: {DateTime.UtcNow:MMMM d, yyyy 'at' h:mm tt} UTC",
                normalFont)
            { SpacingAfter = 16 });

            // Summary table
            var summary = new PdfPTable(4) { WidthPercentage = 100 };
            summary.SetWidths(new float[] { 1, 1, 1, 1 });

            AddSummaryCell(summary, "Pages Compared",
                totalPages.ToString(), headerBg, headingFont, normalFont);
            AddSummaryCell(summary,
                "Words Added",
                diffPages.Sum(p => p.AddedWords).ToString(),
                addedBg, headingFont, addedFont);
            AddSummaryCell(summary,
                "Words Removed",
                diffPages.Sum(p => p.RemovedWords).ToString(),
                removedBg, headingFont, removedFont);
            AddSummaryCell(summary,
                "Unchanged",
                diffPages.Sum(p => p.UnchangedWords).ToString(),
                headerBg, headingFont, normalFont);

            doc.Add(summary);
            doc.Add(new Paragraph(" ") { SpacingAfter = 16 });

            // Legend
            var legend = new Paragraph();
            var addedChunk = new Chunk(" Added ",
        FontFactory.GetFont(FontFactory.HELVETICA, 9,
        new BaseColor(22, 101, 52)));
            addedChunk.SetBackground(addedBg);
            legend.Add(addedChunk);
            legend.Add(new Chunk("   "));
            var removedChunk = new Chunk(" Removed ",
        FontFactory.GetFont(FontFactory.HELVETICA, 9,
        new BaseColor(185, 28, 28)));
            removedChunk.SetBackground(removedBg);
            legend.Add(removedChunk);
            legend.Add(new Chunk("   "));
            legend.Add(new Chunk("Unchanged",
                FontFactory.GetFont(FontFactory.HELVETICA, 9,
                    new BaseColor(60, 60, 60))));
            legend.SpacingAfter = 20;
            doc.Add(legend);

            // Per-page diffs — only pages with changes
            foreach (var page in diffPages)
            {
                if (page.AddedWords == 0 && page.RemovedWords == 0)
                    continue;

                doc.Add(new Paragraph(
                    $"Page {page.PageNumber}  " +
                    $"(+{page.AddedWords} added, " +
                    $"-{page.RemovedWords} removed)",
                    headingFont)
                { SpacingAfter = 6 });

                var para = new Paragraph();
                para.Font = normalFont;

                foreach (var chunk in page.Chunks)
                {
                    if (chunk.Type == DiffType.Unchanged)
                    {
                        para.Add(new Chunk(chunk.Word + " ", normalFont));
                    }
                    else if (chunk.Type == DiffType.Added)
                    {
                        var c = new Chunk(chunk.Word + " ", addedFont);
                        c.SetBackground(addedBg, 1, 1, 1, 1);
                        para.Add(c);
                    }
                    else
                    {
                        var c = new Chunk(chunk.Word + " ", removedFont);
                        c.SetBackground(removedBg, 1, 1, 1, 1);
                        para.Add(c);
                    }
                }

                para.SpacingAfter = 16;
                doc.Add(para);
            }

            if (diffPages.All(p =>
                p.AddedWords == 0 && p.RemovedWords == 0))
            {
                doc.Add(new Paragraph(
                    "✓ No differences found. The documents are identical.",
                    FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12,
                        new BaseColor(22, 101, 52)))
                { SpacingAfter = 12 });
            }

            doc.Close();
            return ms.ToArray();
        }

        private static void AddSummaryCell(
            PdfPTable table,
            string label, string value,
            BaseColor bg,
            Font labelFont, Font valueFont)
        {
            var cell = new PdfPCell();
            cell.BackgroundColor = bg;
            cell.Padding = 10;
            cell.Border = Rectangle.NO_BORDER;
            cell.AddElement(new Paragraph(value, labelFont));
            cell.AddElement(new Paragraph(label, valueFont));
            table.AddCell(cell);
        }
    }

    public class CompareResult
    {
        public byte[] ReportBytes { get; set; } = Array.Empty<byte>();
        public int TotalPagesCompared { get; set; }
        public int TotalAddedWords { get; set; }
        public int TotalRemovedWords { get; set; }
        public int TotalUnchangedWords { get; set; }
        public bool HasDifferences { get; set; }
    }

    public class PageDiff
    {
        public int PageNumber { get; set; }
        public List<DiffChunk> Chunks { get; set; } = new();
        public int AddedWords { get; set; }
        public int RemovedWords { get; set; }
        public int UnchangedWords { get; set; }
    }

    public record DiffChunk(string Word, DiffType Type);

    public enum DiffType { Unchanged, Added, Removed }
}