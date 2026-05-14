using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace PdfToolStack.Application.Strategies
{
    public class PdfToWordStrategy : IProcessingStrategy
    {
        private const string FriendlyConversionFailure =
            "We could not read this PDF for Word conversion. " +
            "If the file is password-protected, corrupt, or scanned, " +
            "unlock it or run OCR first, then try again.";
        private const string OcrRequiredMessage =
            "This PDF appears to contain scanned or image-only pages. " +
            "Run OCR first, then convert the searchable PDF to Word.";
        private const string MissingDocxMessage =
            "The DOCX file could not be generated. Please try again.";

        public ToolType ToolType => ToolType.PdfToWord;

        private readonly IPdfProcessor _processor;
        private readonly ILogger<PdfToWordStrategy> _logger;

        public PdfToWordStrategy(
            IPdfProcessor processor,
            ILogger<PdfToWordStrategy> logger)
        {
            _processor = processor;
            _logger = logger;
        }

        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (request.FileBytes == null
                    || request.FileBytes.Length == 0)
                    return ProcessingResult.Failure(
                        "File is empty or invalid.");

                if (!IsPdf(request.FileBytes))
                    return ProcessingResult.Failure(
                        "File is not a valid PDF.");

                var outputBytes = await _processor.ProcessAsync(
                    request.FileBytes, cancellationToken);

                _logger.LogDebug(
                    "[PdfToWordDiag] Strategy converter output bytes length={OutputBytesLength}",
                    outputBytes?.Length ?? 0);

                if (outputBytes == null || outputBytes.Length == 0)
                    return ProcessingResult.Failure(MissingDocxMessage);

                return ProcessingResult.Success(
                    outputBytes,
                    request.FileSizeBytes);
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains(
                    "scanned or image-only pages",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return ProcessingResult.Failure(OcrRequiredMessage);
                }

                return ProcessingResult.Failure(FriendlyConversionFailure);
            }
            catch (Exception)
            {
                return ProcessingResult.Failure(FriendlyConversionFailure);
            }
        }

        private static bool IsPdf(byte[] bytes)
        {
            return bytes.Length >= 4 &&
                   bytes[0] == 0x25 &&
                   bytes[1] == 0x50 &&
                   bytes[2] == 0x44 &&
                   bytes[3] == 0x46;
        }
    }
}
