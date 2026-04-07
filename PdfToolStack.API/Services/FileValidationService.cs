using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PdfToolStack.API.Configuration;

namespace PdfToolStack.API.Services
{
    public interface IFileValidationService
    {
        Task<(bool IsValid, string? Error)> ValidatePdfAsync(
            IFormFile file,
            bool isPro = false,
            CancellationToken cancellationToken = default);

        Task<(bool IsValid, string? Error)> ValidateFileAsync(
            IFormFile file,
            bool isPro = false,
            CancellationToken cancellationToken = default);
    }

    public sealed class FileValidationService : IFileValidationService
    {
        private static readonly string[] AllowedPdfExtensions = [".pdf"];

        private static readonly string[] AllowedPdfMimeTypes =
        [
            "application/pdf",
            "application/x-pdf",
            "binary/octet-stream"
        ];

        private readonly FileLimit _limits;

        public FileValidationService(IOptions<FileLimit> limits)
        {
            _limits = limits.Value;
        }

        public async Task<(bool IsValid, string? Error)> ValidatePdfAsync(
            IFormFile file,
            bool isPro = false,
            CancellationToken cancellationToken = default)
        {
            var sizeResult = ValidateSize(file, isPro);
            if (!sizeResult.IsValid)
                return sizeResult;

            var fileName = file.FileName?.Trim();
            if (string.IsNullOrWhiteSpace(fileName))
                return (false, "File name is missing.");

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension) ||
                !AllowedPdfExtensions.Contains(
                    extension, StringComparer.OrdinalIgnoreCase))
                return (false, "Only PDF files are allowed.");

            if (!string.IsNullOrWhiteSpace(file.ContentType) &&
                !AllowedPdfMimeTypes.Contains(
                    file.ContentType, StringComparer.OrdinalIgnoreCase))
                return (false, "Invalid file type.");

            await using var stream = file.OpenReadStream();
            if (!stream.CanRead)
                return (false, "Unable to read uploaded file.");

            var header = new byte[5];
            var bytesRead = await stream.ReadAsync(header, cancellationToken);

            var hasPdfHeader =
                bytesRead >= 5 &&
                header[0] == 0x25 && // %
                header[1] == 0x50 && // P
                header[2] == 0x44 && // D
                header[3] == 0x46 && // F
                header[4] == 0x2D;   // -

            if (!hasPdfHeader)
                return (false, "File must be a valid PDF.");

            return (true, null);
        }

        public Task<(bool IsValid, string? Error)> ValidateFileAsync(
            IFormFile file,
            bool isPro = false,
            CancellationToken cancellationToken = default)
        {
            var sizeResult = ValidateSize(file, isPro);
            return Task.FromResult(sizeResult);
        }

        private (bool IsValid, string? Error) ValidateSize(
            IFormFile file,
            bool isPro)
        {
            if (file is null)
                return (false, "No file provided.");

            if (file.Length == 0)
                return (false, "The uploaded file is empty.");

            var maxBytes = isPro
                ? _limits.PaidTierMaxBytes
                : _limits.FreeTierMaxBytes;

            if (file.Length > maxBytes)
            {
                var maxMb = maxBytes / 1024 / 1024;
                var tier = isPro ? "" : " on the free plan";
                return (false,
                    $"File exceeds the {maxMb}MB limit{tier}. " +
                    (isPro ? "" : "Upgrade to Pro for files up to 500MB."));
            }

            return (true, null);
        }
    }
}