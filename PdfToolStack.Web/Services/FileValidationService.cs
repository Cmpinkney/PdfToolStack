using Microsoft.AspNetCore.Components.Forms;

namespace PdfToolStack.Web.Services
{
    public class FileValidationService
    {
        // File size limits
        public const long MaxFreeTierBytes = 10485760;    // 10MB
        public const long MaxPaidTierBytes = 524288000;   // 500MB
        public const int MaxPageCount = 500;

        // Allowed types per tool
        private static readonly HashSet<string> PdfTypes =
            new() { ".pdf", "application/pdf" };

        private static readonly HashSet<string> ImageTypes =
            new() { ".jpg", ".jpeg", ".png", ".bmp",
                    ".gif", ".tiff", ".tif", ".webp" };

        private static readonly HashSet<string> WordTypes =
            new() { ".docx", ".doc" };

        private static readonly HashSet<string> ExcelTypes =
            new() { ".xlsx", ".xls" };

        private static readonly HashSet<string> PptTypes =
            new() { ".pptx", ".ppt" };

        public ValidationResult ValidatePdfFile(
            IBrowserFile file)
        {
            if (file == null)
                return ValidationResult.Fail(
                    "No file selected.");

            if (file.Size == 0)
                return ValidationResult.Fail(
                    "The file appears to be empty.");

            var ext = Path.GetExtension(file.Name)
                .ToLowerInvariant();

            if (!PdfTypes.Contains(ext) &&
                !PdfTypes.Contains(file.ContentType))
                return ValidationResult.Fail(
                    $"Invalid file type. " +
                    $"Please upload a PDF file. " +
                    $"You uploaded: {ext}");

            if (file.Size > MaxPaidTierBytes)
                return ValidationResult.Fail(
                    $"File is too large. " +
                    $"Maximum size is 500MB. " +
                    $"Your file is " +
                    $"{FormatSize(file.Size)}.");

            return ValidationResult.Ok();
        }

        public ValidationResult ValidateImageFile(
            IBrowserFile file)
        {
            if (file == null)
                return ValidationResult.Fail(
                    "No file selected.");

            if (file.Size == 0)
                return ValidationResult.Fail(
                    "The file appears to be empty.");

            var ext = Path.GetExtension(file.Name)
                .ToLowerInvariant();

            if (!ImageTypes.Contains(ext))
                return ValidationResult.Fail(
                    $"Invalid file type. " +
                    $"Please upload an image file " +
                    $"(JPG, PNG, BMP, GIF, TIFF). " +
                    $"You uploaded: {ext}");

            if (file.Size > MaxFreeTierBytes)
                return ValidationResult.Fail(
                    $"Image is too large. " +
                    $"Maximum size is 10MB per image.");

            return ValidationResult.Ok();
        }

        public ValidationResult ValidateWordFile(
            IBrowserFile file)
        {
            if (file == null)
                return ValidationResult.Fail(
                    "No file selected.");

            if (file.Size == 0)
                return ValidationResult.Fail(
                    "The file appears to be empty.");

            var ext = Path.GetExtension(file.Name)
                .ToLowerInvariant();

            if (!WordTypes.Contains(ext))
                return ValidationResult.Fail(
                    $"Invalid file type. " +
                    $"Please upload a Word document " +
                    $"(.docx or .doc). " +
                    $"You uploaded: {ext}");

            return ValidationResult.Ok();
        }

        public ValidationResult ValidateExcelFile(
            IBrowserFile file)
        {
            if (file == null)
                return ValidationResult.Fail(
                    "No file selected.");

            if (file.Size == 0)
                return ValidationResult.Fail(
                    "The file appears to be empty.");

            var ext = Path.GetExtension(file.Name)
                .ToLowerInvariant();

            if (!ExcelTypes.Contains(ext))
                return ValidationResult.Fail(
                    $"Invalid file type. " +
                    $"Please upload an Excel file " +
                    $"(.xlsx or .xls). " +
                    $"You uploaded: {ext}");

            return ValidationResult.Ok();
        }

        public ValidationResult ValidatePptFile(
            IBrowserFile file)
        {
            if (file == null)
                return ValidationResult.Fail(
                    "No file selected.");

            if (file.Size == 0)
                return ValidationResult.Fail(
                    "The file appears to be empty.");

            var ext = Path.GetExtension(file.Name)
                .ToLowerInvariant();

            if (!PptTypes.Contains(ext))
                return ValidationResult.Fail(
                    $"Invalid file type. " +
                    $"Please upload a PowerPoint file " +
                    $"(.pptx or .ppt). " +
                    $"You uploaded: {ext}");

            return ValidationResult.Ok();
        }

        public ValidationResult ValidatePdfBytes(
            byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return ValidationResult.Fail(
                    "The file appears to be empty.");

            // Check PDF magic bytes %PDF
            if (bytes.Length < 4 ||
                bytes[0] != 0x25 ||
                bytes[1] != 0x50 ||
                bytes[2] != 0x44 ||
                bytes[3] != 0x46)
                return ValidationResult.Fail(
                    "The file does not appear to be a " +
                    "valid PDF. It may be corrupted or " +
                    "in the wrong format.");

            // Check for password protection
            var header = System.Text.Encoding.ASCII
                .GetString(bytes, 0,
                    Math.Min(1024, bytes.Length));

            if (header.Contains("/Encrypt"))
                return ValidationResult.Warn(
                    "This PDF is password protected. " +
                    "You may need to unlock it first " +
                    "using our Unlock PDF tool.");

            return ValidationResult.Ok();
        }

        public ValidationResult ValidatePageCount(
            int pageCount)
        {
            if (pageCount > MaxPageCount)
                return ValidationResult.Warn(
                    $"This PDF has {pageCount} pages. " +
                    $"Processing very large PDFs may take " +
                    $"longer than usual. " +
                    $"Consider splitting it first for " +
                    $"faster results.");

            return ValidationResult.Ok();
        }

        public string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{Math.Round((double)bytes / 1024, 1)} KB";
            return $"{Math.Round((double)bytes / (1024 * 1024), 1)} MB";
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; private set; }
        public bool IsWarning { get; private set; }
        public string? Message { get; private set; }

        private ValidationResult(bool isValid,
            bool isWarning = false,
            string? message = null)
        {
            IsValid = isValid;
            IsWarning = isWarning;
            Message = message;
        }

        public static ValidationResult Ok() =>
            new(true);

        public static ValidationResult Warn(
            string message) =>
            new(true, true, message);

        public static ValidationResult Fail(
            string message) =>
            new(false, false, message);
    }
}