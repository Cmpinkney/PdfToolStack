using iTextSharp.text.pdf;
using PdfToolkit.Domain.Interfaces;
using PdfToolkit.Domain.Enums;

namespace PdfToolkit.Infrastructure.Processors
{
    public class ProtectPdfProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.ProtectPdf;

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
            => Task.FromResult(inputBytes);

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            string userPassword,
            string ownerPassword,
            bool allowPrinting = true,
            bool allowCopying = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new PdfReader(inputBytes);
                using var outputStream = new MemoryStream();

                int permissions = 0;
                if (allowPrinting)
                    permissions |= PdfWriter.ALLOW_PRINTING;
                if (allowCopying)
                    permissions |= PdfWriter.ALLOW_COPY;

                using var stamper = new PdfStamper(
                    reader, outputStream);
                stamper.SetEncryption(
                    System.Text.Encoding.UTF8
                        .GetBytes(userPassword),
                    System.Text.Encoding.UTF8
                        .GetBytes(ownerPassword),
                    permissions,
                    PdfWriter.ENCRYPTION_AES_128);
                stamper.Close();

                return Task.FromResult(
                    outputStream.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Protect PDF failed: {ex.Message}");
            }
        }
    }
}