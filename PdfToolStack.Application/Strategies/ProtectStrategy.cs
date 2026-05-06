using PdfToolStack.Application.DTOs;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;

namespace PdfToolStack.Application.Strategies
{
    public class ProtectStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.ProtectPdf;
        private readonly IProtectPdfProcessor _processor;

        public ProtectStrategy(IProtectPdfProcessor processor)
            => _processor = processor;

        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var ownerPassword = string.IsNullOrWhiteSpace(request.OwnerPassword)
                    ? request.UserPassword
                    : request.OwnerPassword;

                if (string.IsNullOrWhiteSpace(request.UserPassword) &&
                    string.IsNullOrWhiteSpace(ownerPassword))
                {
                    return ProcessingResult.Failure(
                        "A password is required to protect the PDF.");
                }

                var output = await _processor.ProcessAsync(
                    request.FileBytes,
                    request.UserPassword,
                    ownerPassword,
                    request.AllowPrinting,
                    request.AllowCopying,
                    cancellationToken);
                return ProcessingResult.Success(output, request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure($"Protect failed: {ex.Message}");
            }
        }
    }
}
