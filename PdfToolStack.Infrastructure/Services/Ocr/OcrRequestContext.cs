namespace PdfToolStack.Infrastructure.Services.Ocr
{
    public sealed record OcrRequestContext(
        string UserId,
        bool IsProUser,
        bool HighAccuracy = false);
}
