using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;

namespace PdfToolStack.Infrastructure.Processors
{
    public class PdfMerger : IPdfProcessor
    {
        public ToolType ToolType => ToolType.MergePdf;

        // Additional files to merge passed in via constructor
        private readonly IEnumerable<byte[]> _additionalFiles;

        public PdfMerger(
            IEnumerable<byte[]>? additionalFiles = null)
        {
            _additionalFiles = additionalFiles
                ?? Enumerable.Empty<byte[]>();
        }

        public async Task<byte[]> ProcessAsync(
            byte[] fileBytes,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                using var outputStream = new MemoryStream();

                var document = new Document();
                var writer = new PdfCopy(
                    document, outputStream);

                document.Open();

                // Merge primary file
                MergeFile(fileBytes, writer,
                    cancellationToken);

                // Merge additional files
                foreach (var file in _additionalFiles)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();
                    MergeFile(file, writer,
                        cancellationToken);
                }

                document.Close();
                return outputStream.ToArray();

            }, cancellationToken);
        }

        private static void MergeFile(
            byte[] fileBytes,
            PdfCopy writer,
            CancellationToken cancellationToken)
        {
            using var stream = new MemoryStream(fileBytes);
            var reader = new PdfReader(stream);

            for (int i = 1; i <= reader.NumberOfPages; i++)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();
                writer.AddPage(
                    writer.GetImportedPage(reader, i));
            }

            reader.Close();
        }
    }
}