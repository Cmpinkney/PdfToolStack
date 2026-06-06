namespace PdfToolStack.Application.DTOs;

public class FraudAnalysisResult
{
    public int RiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public List<FraudFlagDto> Flags { get; set; } = new();
    public bool IsServiceError { get; set; }

    public static FraudAnalysisResult ServiceUnavailable() => new()
    {
        IsServiceError = true,
        RiskScore = 0,
        RiskLevel = "UNKNOWN",
        Recommendation = "Service temporarily unavailable. Please try again.",
        Flags = new()
    };
}

public class FraudFlagDto
{
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public class FraudAnalysisHistoryItem
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime InvoiceDate { get; set; }
    public string Notes { get; set; } = string.Empty;
}
