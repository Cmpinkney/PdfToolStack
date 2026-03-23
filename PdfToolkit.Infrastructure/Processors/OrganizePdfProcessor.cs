using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfToolkit.Domain.Interfaces;
using PdfToolkit.Domain.Enums;
using PdfToolkit.Domain.Entities;

namespace PdfToolkit.Infrastructure.Processors
{
    public class OrganizePdfProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.OrganizePdf;

        public Task<byte[]> ProcessAsync(
    byte[] inputBytes,
    CancellationToken cancellationToken = default)
        {
            return Task.FromResult(inputBytes);
        }

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            IEnumerable<PageOperation> operations,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new PdfReader(inputBytes);
                int totalPages = reader.NumberOfPages;

                var pageOrder = Enumerable
                    .Range(1, totalPages).ToList();

                foreach (var op in operations
                    .OrderBy(o => o.Order))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    switch (op.Type)
                    {
                        case "delete":
                            pageOrder.Remove(op.PageNumber);
                            break;
                        case "move":
                            if (pageOrder.Contains(op.PageNumber) &&
                                op.TargetIndex >= 0 &&
                                op.TargetIndex < pageOrder.Count)
                            {
                                pageOrder.Remove(op.PageNumber);
                                pageOrder.Insert(
                                    op.TargetIndex, op.PageNumber);
                            }
                            break;
                    }
                }

                if (pageOrder.Count == 0)
                    throw new Exception("Cannot delete all pages.");

                using var outputStream = new MemoryStream();
                using var doc = new Document(
                    reader.GetPageSizeWithRotation(pageOrder[0]));
                using var copy = new PdfCopy(doc, outputStream);

                doc.Open();
                foreach (var pageNum in pageOrder)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var rotation = operations
                        .FirstOrDefault(o =>
                            o.Type == "rotate" &&
                            o.PageNumber == pageNum)
                        ?.RotationDegrees ?? 0;

                    if (rotation != 0)
                    {
                        var dict = reader.GetPageN(pageNum);
                        var current = dict
                            .GetAsNumber(PdfName.Rotate)
                            ?.IntValue ?? 0;
                        dict.Put(PdfName.Rotate,
                            new PdfNumber(
                                (current + rotation) % 360));
                    }

                    copy.AddPage(copy.GetImportedPage(
                        reader, pageNum));
                }
                doc.Close();

                return Task.FromResult(outputStream.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Organize PDF failed: {ex.Message}");
            }
        }
    }

    public class PageOperation
    {
        public int Order { get; set; }
        public string Type { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public int TargetIndex { get; set; }
        public int RotationDegrees { get; set; }
    }
}