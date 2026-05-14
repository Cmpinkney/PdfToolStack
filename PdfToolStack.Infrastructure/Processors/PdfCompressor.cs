using iTextSharp.text.pdf;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;
using SkiaSharp;

namespace PdfToolStack.Infrastructure.Processors
{
    public class PdfCompressor : ICompressProcessor
    {
        public ToolType ToolType => ToolType.CompressPdf;

        private sealed record ProfileSettings(int JpegQuality, bool RecompressImages, bool Downscale);

        private static ProfileSettings GetSettings(CompressionProfile profile) => profile switch
        {
            CompressionProfile.Email       => new(40, true,  true),
            CompressionProfile.HighQuality => new(85, false, false),
            _                              => new(65, true,  false), // Balanced
        };

        // IPdfProcessor — defaults to Balanced
        public Task<byte[]> ProcessAsync(
            byte[] fileBytes,
            CancellationToken cancellationToken = default) =>
            ProcessAsync(fileBytes, CompressionProfile.Balanced, cancellationToken);

        // ICompressProcessor — profile-aware
        public async Task<byte[]> ProcessAsync(
            byte[] fileBytes,
            CompressionProfile profile,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var settings = GetSettings(profile);

                using var inputStream = new MemoryStream(fileBytes);
                using var outputStream = new MemoryStream();

                using var reader = new PdfReader(inputStream);

                if (settings.RecompressImages)
                    RecompressJpegImages(reader, settings, cancellationToken);

                using var stamper = new PdfStamper(reader, outputStream);
                stamper.SetFullCompression();

                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var pageContent = reader.GetPageContent(i);
                    reader.SetPageContent(i, pageContent);
                }

                stamper.Close();

                var outputBytes = outputStream.ToArray();
                if (outputBytes.Length == 0)
                    throw new InvalidOperationException("Compressed PDF output was empty.");

                return outputBytes;
            }, cancellationToken);
        }

        private static void RecompressJpegImages(PdfReader reader, ProfileSettings settings, CancellationToken ct)
        {
            for (int i = 0; i < reader.XrefSize; i++)
            {
                ct.ThrowIfCancellationRequested();

                var pdfObj = reader.GetPdfObject(i);
                if (pdfObj is not PrStream stream) continue;

                if (!PdfName.Image.Equals(stream.GetAsName(PdfName.Subtype))) continue;

                // Only handle JPEG-encoded images; skip masks and sub-8-bit images
                if (!PdfName.Dctdecode.Equals(stream.Get(PdfName.Filter))) continue;
                if ((stream.GetAsNumber(PdfName.Bitspercomponent)?.IntValue ?? 0) < 8) continue;
                if (stream.GetAsBoolean(PdfName.Imagemask)?.BooleanValue == true) continue;

                try
                {
                    var jpegBytes = PdfReader.GetStreamBytesRaw(stream);
                    if (jpegBytes == null || jpegBytes.Length < 100) continue;

                    using var bitmap = SKBitmap.Decode(jpegBytes);
                    if (bitmap == null) continue;

                    SKBitmap workBitmap = bitmap;
                    SKBitmap? resized = null;
                    try
                    {
                        if (settings.Downscale)
                        {
                            // Downsample images over ~1.4 MP (roughly 96 dpi on an A4 page)
                            const long MaxPixels = 1_440_000L;
                            var total = (long)bitmap.Width * bitmap.Height;
                            if (total > MaxPixels)
                            {
                                var scale = Math.Sqrt((double)MaxPixels / total);
                                var nw = Math.Max(64, (int)(bitmap.Width * scale));
                                var nh = Math.Max(64, (int)(bitmap.Height * scale));
                                resized = bitmap.Resize(
                                    new SKImageInfo(nw, nh),
                                    new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
                                if (resized != null) workBitmap = resized;
                            }
                        }

                        using var skImage = SKImage.FromBitmap(workBitmap);
                        using var encoded = skImage.Encode(SKEncodedImageFormat.Jpeg, settings.JpegQuality);
                        if (encoded == null) continue;

                        var newBytes = encoded.ToArray();
                        if (newBytes.Length >= jpegBytes.Length) continue;

                        // SetData(_, false) clears /Filter and sets /Length automatically
                        stream.SetData(newBytes, false);
                        stream.Put(PdfName.Filter, PdfName.Dctdecode);
                        stream.Remove(PdfName.Decodeparms);

                        if (!ReferenceEquals(workBitmap, bitmap))
                        {
                            stream.Put(PdfName.Width, new PdfNumber(workBitmap.Width));
                            stream.Put(PdfName.Height, new PdfNumber(workBitmap.Height));
                        }
                    }
                    finally
                    {
                        resized?.Dispose();
                    }
                }
                catch
                {
                    // Leave original image on any error — never corrupt the PDF
                }
            }
        }
    }
}
