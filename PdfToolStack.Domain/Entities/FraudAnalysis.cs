namespace PdfToolStack.Domain.Entities
{
    public class FraudAnalysis
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public decimal InvoiceAmount { get; set; }
        public string Currency { get; set; } = "USD";
        public DateTime InvoiceDate { get; set; }
        public int RiskScore { get; set; }        // 0-100
        public string RiskLevel { get; set; } = string.Empty;  // LOW / MEDIUM / HIGH / CRITICAL
        public string FlagsJson { get; set; } = string.Empty;  // serialized list of FraudFlag
        public string Recommendation { get; set; } = string.Empty;
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
        public string RawInvoiceJson { get; set; } = string.Empty; // the extracted invoice data
    }

    public class FraudFlag
    {
        public string Category { get; set; } = string.Empty;   // BANK_CHANGE / AMOUNT_ANOMALY / DATE_ANOMALY / MATH_ERROR / SEQUENCE_GAP / DUPLICATE
        public string Severity { get; set; } = string.Empty;   // CRITICAL / HIGH / MEDIUM / LOW
        public string Description { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }
}
