using PdfToolStack.Application.DTOs;
using PdfToolStack.Application.Strategies;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Infrastructure.Processors;

namespace PdfToolStack.Infrastructure.Strategies
{
    public class CropStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.CropPdf;

        private readonly CropPdfProcessor _processor;

        public CropStrategy(CropPdfProcessor processor)
            => _processor = processor;

        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var output = await _processor.ProcessAsync(
                    request.FileBytes,
                    request.CropMarginTop,
                    request.CropMarginRight,
                    request.CropMarginBottom,
                    request.CropMarginLeft,
                    request.CropPageNumbers,
                    cancellationToken);

                return ProcessingResult.Success(output, request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure($"Crop failed: {ex.Message}");
            }
        }
    }
}