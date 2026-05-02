using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace PdfToolStack.Infrastructure.Storage
{
    public class AzureBlobStorageService : IBlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName = "pdf-outputs";

        public AzureBlobStorageService(
            BlobServiceClient blobServiceClient)
        {
            _blobServiceClient = blobServiceClient;
        }

        public async Task<string> UploadAsync(
            byte[] fileBytes,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            var containerClient = _blobServiceClient
                .GetBlobContainerClient(_containerName);

            await containerClient.CreateIfNotExistsAsync(
                PublicAccessType.None,
                cancellationToken: cancellationToken);

            // Unique blob name — prevents collisions
            var blobName = $"{Guid.NewGuid()}/{fileName}";
            var blobClient = containerClient.GetBlobClient(blobName);

            using var stream = new MemoryStream(fileBytes);
            await blobClient.UploadAsync(
                stream,
                overwrite: true,
                cancellationToken: cancellationToken);

            // Generate SAS URL valid for 1 hour only
            var sasUri = blobClient.GenerateSasUri(
                BlobSasPermissions.Read,
                DateTimeOffset.UtcNow.AddHours(1));

            return sasUri.ToString();
        }

        public async Task DeleteAsync(
            string blobUrl,
            CancellationToken cancellationToken = default)
        {
            var containerClient = _blobServiceClient
                .GetBlobContainerClient(_containerName);

            var blobName = GetBlobName(blobUrl);
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync(
                cancellationToken: cancellationToken);
        }

        public async Task<byte[]?> DownloadAsync(
            string blobUrl,
            CancellationToken cancellationToken = default)
        {
            var containerClient = _blobServiceClient
                .GetBlobContainerClient(_containerName);

            var blobName = GetBlobName(blobUrl);
            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync(cancellationToken))
                return null;

            using var stream = new MemoryStream();
            await blobClient.DownloadToAsync(stream, cancellationToken);
            return stream.ToArray();
        }

        private string GetBlobName(string blobUrl)
        {
            var uri = new Uri(blobUrl);
            var path = uri.AbsolutePath.TrimStart('/');
            var prefix = _containerName + "/";
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? path[prefix.Length..]
                : path;
        }
    }
}
