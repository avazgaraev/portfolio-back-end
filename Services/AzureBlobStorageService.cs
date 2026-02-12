using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using Vertex.Settings;

namespace Vertex.Services
{
    public class AzureBlobStorageService
    {
        private readonly BlobServiceClient _serviceClient;
        private readonly AzureStorageOptions _opt;

        public AzureBlobStorageService(IOptions<AzureStorageOptions> options)
        {
            _opt = options.Value;
            _serviceClient = new BlobServiceClient(_opt.ConnectionString);
        }

        public async Task<List<string>> UploadAsync(List<IFormFile> files, CancellationToken ct = default)
        {
            var container = _serviceClient.GetBlobContainerClient(_opt.ContainerName);
            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
            // ✅ PublicAccessType.None: təhlükəsiz. Linklər üçün SAS yaradacağıq.

            var urls = new List<string>();

            foreach (var file in files)
            {
                if (file == null || file.Length == 0) continue;

                var ext = Path.GetExtension(file.FileName);
                var safeExt = string.IsNullOrWhiteSpace(ext) ? "" : ext.ToLowerInvariant();

                // sadə whitelist
                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg",".jpeg",".png",".webp",".gif",".mp4",".mov",".webm" };

                if (!allowed.Contains(safeExt))
                    throw new InvalidOperationException($"File type not allowed: {safeExt}");

                var blobName = $"uploads/{DateTime.UtcNow:yyyy}/{DateTime.UtcNow:MM}/{Guid.NewGuid():N}{safeExt}";
                var blob = container.GetBlobClient(blobName);

                var contentType = file.ContentType;
                var headers = new BlobHttpHeaders
                {
                    ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType
                };

                await using var stream = file.OpenReadStream();
                await blob.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers }, ct);

                // ✅ SAS URL (müddət: 30 gün — IG/FB rahat götürsün)
                var sasUrl = GetBlobReadSasUrl(blob, daysValid: 30);
                urls.Add(sasUrl);
            }

            if (urls.Count == 0)
                throw new InvalidOperationException("No valid files uploaded.");

            return urls;
        }

        private static string GetBlobReadSasUrl(BlobClient blob, int daysValid)
        {
            // SAS üçün blob client-in credential-lı olması lazımdır (connection string ilə olur)
            var sas = new BlobSasBuilder
            {
                BlobContainerName = blob.BlobContainerName,
                BlobName = blob.Name,
                Resource = "b", // blob
                ExpiresOn = DateTimeOffset.UtcNow.AddDays(daysValid)
            };

            sas.SetPermissions(BlobSasPermissions.Read);

            var sasUri = blob.GenerateSasUri(sas);
            return sasUri.ToString();
        }
    }
}
