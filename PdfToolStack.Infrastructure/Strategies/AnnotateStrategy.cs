using iTextSharp.text;
using PdfToolStack.Application.DTOs;
using PdfToolStack.Application.Strategies;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Infrastructure.Strategies
{
    public class AnnotateStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.AnnotatePdf;

        private readonly Processors.AnnotatePdfProcessor _processor;

        public AnnotateStrategy(Processors.AnnotatePdfProcessor processor)
            => _processor = processor;

        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var highlights = request.Highlights
                    .Select(h => new Processors.PdfHighlight
                    {
                        Type = h.Type,
                        PageNumber = h.PageNumber,
                        X = h.X,
                        Y = h.Y,
                        Width = h.Width,
                        Height = h.Height,
                        LineWidth = h.LineWidth,
                        StrokeColor = ParseColor(h.Color),
                        Points = h.Points?
                            .Select(p => new Processors.PointF { X = p.X, Y = p.Y })
                            .ToList() ?? new()
                    })
                    .ToList();

                var output = await _processor.ProcessAsync(
                    request.FileBytes, highlights, cancellationToken);

                return ProcessingResult.Success(output, request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure($"Annotate failed: {ex.Message}");
            }
        }

        private static BaseColor ParseColor(string? hex)
        {
            if (string.IsNullOrEmpty(hex)) return new BaseColor(0, 0, 0);
            hex = hex.TrimStart('#');
            if (hex.Length < 6) return new BaseColor(0, 0, 0);
            return new BaseColor(
                Convert.ToInt32(hex[..2], 16),
                Convert.ToInt32(hex[2..4], 16),
                Convert.ToInt32(hex[4..6], 16));
        }
    }
}