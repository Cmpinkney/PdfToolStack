using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdfToolStack.Infrastructure.Storage
{
    public interface IBlobStorageService
    {
        Task<string> UploadAsync(
            byte[] fileBytes,
            string fileName,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            string blobUrl,
            CancellationToken cancellationToken = default);

        Task<byte[]?> DownloadAsync(
            string blobUrl,
            CancellationToken cancellationToken = default);
    }
}
