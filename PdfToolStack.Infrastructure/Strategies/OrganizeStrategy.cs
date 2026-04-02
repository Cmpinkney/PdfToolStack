using PdfToolStack.Application.DTOs;
using PdfToolStack.Application.Strategies;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Infrastructure.Strategies
{
    public class OrganizeStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.OrganizePdf;

        private readonly Processors.OrganizePdfProcessor _processor;

        public OrganizeStrategy(Processors.OrganizePdfProcessor processor)
            => _processor = processor;

        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var operations = request.PageOperations
                    .Select(o => new Processors.PageOperation
                    {
                        Order = o.Order,
                        Type = o.Type,
                        PageNumber = o.PageNumber,
                        TargetIndex = o.TargetIndex,
                        RotationDegrees = o.RotationDegrees
                    })
                    .ToList();

                var output = await _processor.ProcessAsync(
                    request.FileBytes, operations, cancellationToken);

                return ProcessingResult.Success(output, request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure($"Organize failed: {ex.Message}");
            }
        }
    }
}