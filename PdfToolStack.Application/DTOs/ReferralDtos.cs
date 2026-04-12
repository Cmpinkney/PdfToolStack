namespace PdfToolStack.Application.DTOs
{
    public class ReferralCodeDto
    {
        public string Code { get; set; } = string.Empty;
    }

    public class ReferralStatsDto
    {
        public int Total { get; set; }
        public int Converted { get; set; }
        public int Rewarded { get; set; }
    }
}