using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PdfToolStack.API.Configuration;

namespace PdfToolStack.API.Services
{
    public interface IFileValidationService
    {
        Task<(bool IsValid, string? Error)> ValidatePdfAsync(
            IFormFile file,
            CancellationToken cancellationToken = default);
    }

    public sealed class FileValidationService : IFileValidationService
    {
        private static readonly string[] AllowedExtensions =
        [
            ".pdf"
        ];

        private static readonly string[] AllowedMimeTypes =
        [
            "application/pdf",
            "application/x-pdf",
            "binary/octet-stream"
        ];

        private readonly ProcessingOptions _options;

        public FileValidationService(IOptions<ProcessingOptions> options)
        {
            _options = options.Value;
        }

        public async Task<(bool IsValid, string? Error)> ValidatePdfAsync(
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            if (file == null)
                return (false, "No file provided.");

            if (file.Length == 0)
                return (false, "The uploaded file is empty.");

            if (file.Length > _options.MaxFileSizeBytes)
            {
                var maxMb = _options.MaxFileSizeBytes / 1024 / 1024;
                return (false, $"File exceeds maximum size of {maxMb}MB.");
            }

            var fileName = file.FileName?.Trim();
            if (string.IsNullOrWhiteSpace(fileName))
                return (false, "File name is missing.");

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension) ||
                !AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return (false, "Only PDF files are allowed.");
            }

            if (!string.IsNullOrWhiteSpace(file.ContentType) &&
                !AllowedMimeTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                return (false, "Invalid file type.");
            }

            await using var stream = file.OpenReadStream();

            if (!stream.CanRead)
                return (false, "Unable to read uploaded file.");

            var headerBuffer = new byte[5];
            var bytesRead = await stream.ReadAsync(headerBuffer, cancellationToken);

            var hasPdfHeader =
                bytesRead >= 5 &&
                headerBuffer[0] == 0x25 && // %
                headerBuffer[1] == 0x50 && // P
                headerBuffer[2] == 0x44 && // D
                headerBuffer[3] == 0x46 && // F
                headerBuffer[4] == 0x2D;   // -

            if (!hasPdfHeader)
                return (false, "File must be a valid PDF.");

            return (true, null);
        }
    }
}