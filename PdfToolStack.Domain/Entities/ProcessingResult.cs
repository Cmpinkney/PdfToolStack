namespace PdfToolStack.Domain.Entities
{
    public class ProcessingResult
    {
        public bool IsSuccess { get; private set; }
        public byte[]? OutputBytes { get; private set; }
        public string? ErrorMessage { get; private set; }
        public long OriginalSizeBytes { get; private set; }
        public long OutputSizeBytes { get; private set; }

        public static ProcessingResult Success(
            byte[] outputBytes,
            long originalSizeBytes)
        {
            return new ProcessingResult
            {
                IsSuccess = true,
                OutputBytes = outputBytes,
                OriginalSizeBytes = originalSizeBytes,
                OutputSizeBytes = outputBytes.Length
            };
        }

        public static ProcessingResult Failure(
            string errorMessage)
        {
            return new ProcessingResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }
}