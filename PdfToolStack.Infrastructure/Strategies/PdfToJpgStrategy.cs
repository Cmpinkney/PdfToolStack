using PdfToolStack.Application.DTOs;
using PdfToolStack.Application.Strategies;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Infrastructure.Processors;

namespace PdfToolStack.Infrastructure.Strategies
{
    public class PdfToJpgStrategy : IProcessingStrategy
    {
        private readonly PdfToJpgProcessor _processor;

        public PdfToJpgStrategy(PdfToJpgProcessor processor)
        {
            _processor = processor;
        }

        public ToolType ToolType => ToolType.PdfToJpg;

        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var outputBytes = await _processor.ProcessAsync(
                    request.FileBytes, cancellationToken);

                return ProcessingResult.Success(
                    outputBytes, request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure(
                    $"PDF to JPG failed: {ex.Message}");
            }
        }
    }
}