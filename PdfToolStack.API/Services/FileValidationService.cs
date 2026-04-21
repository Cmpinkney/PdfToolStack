using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PdfToolStack.API.Configuration;
using PdfToolStack.Application.Interfaces;

namespace PdfToolStack.API.Services
{
    public interface IFileValidationService
    {
        Task<(bool IsValid, string? Error)> ValidatePdfAsync(
            IFormFile file,
            string? userId = null,
            bool isPro = false,
            CancellationToken cancellationToken = default);

        Task<(bool IsValid, string? Error)> ValidateFileAsync(
            IFormFile file,
            string? userId = null,
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
        private readonly IFeatureAccessService _featureAccessService;

        public FileValidationService(
            IOptions<FileLimit> limits,
            IFeatureAccessService featureAccessService)
        {
            _limits = limits.Value;
            _featureAccessService = featureAccessService;
        }

        public async Task<(bool IsValid, string? Error)> ValidatePdfAsync(
            IFormFile file,
            string? userId = null,
            bool isPro = false,
            CancellationToken cancellationToken = default)
        {
            var sizeResult = await ValidateSizeAsync(file, userId, isPro);
            if (!sizeResult.IsValid)
                return sizeResult;

            var fileName = file.FileName?.Trim();
            if (string.IsNullOrWhiteSpace(fileName))
                return (false, "File name is missing.");

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension) ||
                !AllowedPdfExtensions.Contains(
                    extension, StringComparer.OrdinalIgnoreCase))
            {
                return (false, "Only PDF files are allowed.");
            }

            if (!string.IsNullOrWhiteSpace(file.ContentType) &&
                !AllowedPdfMimeTypes.Contains(
                    file.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                return (false, "Invalid file type.");
            }

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

        public async Task<(bool IsValid, string? Error)> ValidateFileAsync(
            IFormFile file,
            string? userId = null,
            bool isPro = false,
            CancellationToken cancellationToken = default)
        {
            return await ValidateSizeAsync(file, userId, isPro);
        }

        private async Task<(bool IsValid, string? Error)> ValidateSizeAsync(
            IFormFile file,
            string? userId,
            bool isPro)
        {
            if (file is null)
                return (false, "No file provided.");

            if (file.Length == 0)
                return (false, "The uploaded file is empty.");

            var paidMaxBytes = _limits.PaidTierMaxBytes;
            var freeMaxBytes = _limits.FreeTierMaxBytes;

            // Pro users can use the paid tier size limit
            if (isPro)
            {
                if (file.Length > paidMaxBytes)
                {
                    var maxMb = paidMaxBytes / 1024 / 1024;
                    return (false, $"File exceeds the {maxMb}MB Pro limit.");
                }

                return (true, null);
            }

            // Free-tier size check
            if (file.Length > freeMaxBytes)
            {
                // Large file unlock allows a free user to process one oversized file
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    var hasLargeFileUnlock =
                        await _featureAccessService.HasLargeFileUnlockAsync(userId);

                    if (hasLargeFileUnlock)
                    {
                        if (file.Length > paidMaxBytes)
                        {
                            var maxMb = paidMaxBytes / 1024 / 1024;
                            return (false,
                                $"File exceeds the {maxMb}MB paid limit.");
                        }

                        return (true, null);
                    }
                }

                var freeMb = freeMaxBytes / 1024 / 1024;
                return (false,
                    $"File exceeds the {freeMb}MB limit on the free plan. " +
                    "Upgrade to Pro or use a one-time Large File Unlock for files up to 500MB.");
            }

            return (true, null);
        }
    }
}