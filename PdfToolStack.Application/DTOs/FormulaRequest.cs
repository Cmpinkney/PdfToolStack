namespace PdfToolStack.Application.DTOs;

public sealed record FormulaRequest(
    string Prompt,
    string Platform = "Excel");
