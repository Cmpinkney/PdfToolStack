namespace PdfToolStack.Application.Interfaces;

public interface IFormulaAiProvider
{
    Task<string> GenerateFormulaAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken);
}
