namespace PdfToolStack.Infrastructure.Storage
{
    public sealed class MissingBlobStorageService : IBlobStorageService
    {
        private const string Message =
            "Azure Blob Storage is not configured. Set AzureStorage:ConnectionString before saving pending batch files.";

        public Task<string> UploadAsync(
            byte[] fileBytes,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(Message);
        }

        public Task DeleteAsync(
            string blobUrl,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<byte[]?> DownloadAsync(
            string blobUrl,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(Message);
        }
    }
}
