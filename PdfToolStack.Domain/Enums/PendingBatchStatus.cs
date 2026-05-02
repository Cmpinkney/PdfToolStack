namespace PdfToolStack.Domain.Enums
{
    public enum PendingBatchStatus
    {
        PendingPayment = 0,
        Paid = 1,
        Processing = 2,
        Completed = 3,
        Failed = 4,
        Expired = 5
    }
}
