using PdfToolStack.Application.DTOs;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;

namespace PdfToolStack.Application.Strategies
{
    public class WatermarkStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.WatermarkPdf;
        private readonly IWatermarkProcessor _processor;

        public WatermarkStrategy(IWatermarkProcessor processor)
            => _processor = processor;

        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var text = string.IsNullOrWhiteSpace(request.WatermarkText)
                    ? "CONFIDENTIAL"
                    : request.WatermarkText;

                var output = await _processor.ProcessAsync(
                    request.FileBytes,
                    text,
                    request.WatermarkOpacity,
                    request.WatermarkFontSize,
                    cancellationToken);

                return ProcessingResult.Success(output, request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure($"Watermark failed: {ex.Message}");
            }
        }
    }
}