using PdfToolStack.Application.DTOs;
using PdfToolStack.Application.Strategies;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Infrastructure.Processors;

namespace PdfToolStack.Infrastructure.Strategies
{
    public class JpgToPdfStrategy : IProcessingStrategy
    {
        private readonly JpgToPdfProcessor _processor;

        public JpgToPdfStrategy(JpgToPdfProcessor processor)
        {
            _processor = processor;
        }

        public ToolType ToolType => ToolType.JpgToPdf;

        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var allImages = new List<byte[]> { request.FileBytes };
                if (request.AdditionalFiles?.Any() == true)
                    allImages.AddRange(request.AdditionalFiles);

                var outputBytes = await _processor.ProcessAsync(
                    allImages, cancellationToken);

                return ProcessingResult.Success(
                    outputBytes,
                    request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure(
                    $"JPG to PDF failed: {ex.Message}");
            }
        }
    }
}