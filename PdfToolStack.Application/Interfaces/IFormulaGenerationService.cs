using PdfToolStack.Application.DTOs;

namespace PdfToolStack.Application.Interfaces;

public interface IFormulaGenerationService
{
    Task<FormulaResponse> GenerateAsync(
        FormulaRequest request,
        CancellationToken cancellationToken);
}
