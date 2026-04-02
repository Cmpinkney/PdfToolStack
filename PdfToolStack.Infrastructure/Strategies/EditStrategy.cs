using iTextSharp.text;
using PdfToolStack.Application.DTOs;
using PdfToolStack.Application.Strategies;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;
using InfraAnnotation = PdfToolStack.Infrastructure.Processors.PdfAnnotation;

namespace PdfToolStack.Infrastructure.Strategies
{
    public class EditStrategy : IProcessingStrategy
    {
        public ToolType ToolType => ToolType.EditPdf;

        private readonly Processors.EditPdfProcessor _processor;

        public EditStrategy(Processors.EditPdfProcessor processor)
            => _processor = processor;

        public async Task<ProcessingResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var annotations = request.Annotations
                    .Select(a => new InfraAnnotation
                    {
                        Type = a.Type,
                        PageNumber = a.PageNumber,
                        X = a.X,
                        Y = a.Y,
                        X2 = a.X2,
                        Y2 = a.Y2,
                        Width = a.Width,
                        Height = a.Height,
                        Text = a.Text,
                        FontSize = a.FontSize,
                        LineWidth = a.LineWidth,
                        Color = ParseColor(a.Color)
                    })
                    .ToList();

                var output = await _processor.ProcessAsync(
                    request.FileBytes, annotations, cancellationToken);

                return ProcessingResult.Success(output, request.FileSizeBytes);
            }
            catch (Exception ex)
            {
                return ProcessingResult.Failure($"Edit failed: {ex.Message}");
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