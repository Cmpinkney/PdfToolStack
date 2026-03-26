using iTextSharp.text.pdf;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Infrastructure.Processors
{
    public class UnlockPdfProcessor : IPdfProcessor
    {
        public ToolType ToolType => ToolType.UnlockPdf;

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            CancellationToken cancellationToken = default)
            => ProcessAsync(inputBytes, null,
                cancellationToken);

        public Task<byte[]> ProcessAsync(
            byte[] inputBytes,
            string? password,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var passwordBytes = password != null
                    ? System.Text.Encoding.UTF8
                        .GetBytes(password)
                    : null;

                using var reader = passwordBytes != null
                    ? new PdfReader(inputBytes,
                        passwordBytes)
                    : new PdfReader(inputBytes);

                reader.RemoveUsageRights();

                using var outputStream = new MemoryStream();
                using var stamper = new PdfStamper(
                    reader, outputStream);
                stamper.Close();

                return Task.FromResult(
                    outputStream.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Unlock PDF failed: {ex.Message}");
            }
        }
    }
}