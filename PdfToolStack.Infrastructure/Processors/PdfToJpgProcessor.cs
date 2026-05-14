using Docnet.Core;
using Docnet.Core.Models;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace PdfToolStack.Infrastructure.Processors
{
    public class PdfToJpgProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.PdfToJpg;

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
        {
            // Returns first page as JPG — multi-page returns ZIP
            var pages = RenderPages(inputBytes, cancellationToken);
            if (pages.Count == 1)
                return Task.FromResult(pages[0]);

            // Multiple pages — zip them
            using var zipStream = new System.IO.MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(
                zipStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                for (int i = 0; i < pages.Count; i++)
                {
                    var entry = archive.CreateEntry(
                        $"page_{i + 1}.jpg",
                        System.IO.Compression.CompressionLevel.Fastest);
                    using var entryStream = entry.Open();
                    entryStream.Write(pages[i], 0, pages[i].Length);
                }
            }
            zipStream.Position = 0;
            return Task.FromResult(zipStream.ToArray());
        }

        public List<byte[]> RenderPages(
            byte[] pdfBytes,
            CancellationToken cancellationToken = default)
        {
            var results = new List<byte[]>();

            using var lib = DocLib.Instance;
            using var doc = lib.GetDocReader(
                pdfBytes, new PageDimensions(1080, 1920));

            var pageCount = doc.GetPageCount();

            for (int i = 0; i < pageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var pageReader = doc.GetPageReader(i);
                var width = pageReader.GetPageWidth();
                var height = pageReader.GetPageHeight();
                var rawBytes = pageReader.GetImage();

                using var image = Image.LoadPixelData<Bgra32>(
                    rawBytes, width, height);
                using var ms = new System.IO.MemoryStream();
                image.Save(ms, new JpegEncoder());
                results.Add(ms.ToArray());
            }

            return results;
        }
    }
}
