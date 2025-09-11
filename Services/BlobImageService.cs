using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using SmartGallery.Models;

namespace SmartGallery.Services
{
    public class BlobImageService
    {
        private readonly BlobServiceClient _serviceClient;
        private readonly BlobContainerClient _container;
        private readonly StorageOptions _opts;
        private readonly string _accountName;

        private record Meta(string id, string description, DateTimeOffset uploadedAt, string blobName, string contentType);

        public BlobImageService(IOptions<StorageOptions> options)
        {
            _opts = options.Value;

            if (_opts.UseManagedIdentity)
            {
                if (string.IsNullOrWhiteSpace(_opts.AccountName))
                    throw new InvalidOperationException("Storage:AccountName skal sættes ved UseManagedIdentity=true");

                var uri = new Uri($"https://{_opts.AccountName}.blob.core.windows.net");
                TokenCredential cred = new DefaultAzureCredential();
                _serviceClient = new BlobServiceClient(uri, cred);
                _accountName = _opts.AccountName;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_opts.ConnectionString))
                    throw new InvalidOperationException("Storage:ConnectionString mangler");

                _serviceClient = new BlobServiceClient(_opts.ConnectionString);
                _accountName = _serviceClient.Uri.Host.Split('.')[0];
            }

            _container = _serviceClient.GetBlobContainerClient(_opts.ContainerName);
        }

        public async Task EnsureContainerAsync()
        {
            await _container.CreateIfNotExistsAsync(PublicAccessType.None);
        }

        public static bool IsSupportedContentType(string contentType)
            => contentType.StartsWith("image/");

        public async Task<string> UploadAsync(Stream fileStream, string originalFileName, string contentType, string description)
        {
            if (!IsSupportedContentType(contentType))
                throw new InvalidOperationException("Kun billedfiler kan uploades");

            var id = Guid.NewGuid().ToString("N");
            var ext = GetSafeExtension(originalFileName, contentType);
            var blobName = $"items/{id}{ext}";
            var metaName = $"items/{id}.json";

            var blob = _container.GetBlobClient(blobName);
            await blob.UploadAsync(fileStream, new BlobHttpHeaders { ContentType = contentType });

            var meta = new Meta(id, description, DateTimeOffset.UtcNow, blobName, contentType);
            var metaBlob = _container.GetBlobClient(metaName);
            using var metaStream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(meta));
            await metaBlob.UploadAsync(metaStream, new BlobHttpHeaders { ContentType = "application/json" });

            return id;
        }

        public async Task<IReadOnlyList<ImageItem>> ListAsync()
        {
            var results = new List<ImageItem>();

            await foreach (var blobItem in _container.GetBlobsAsync(prefix: "items/"))
            {
                if (!blobItem.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                var metaBlob = _container.GetBlobClient(blobItem.Name);
                var download = await metaBlob.DownloadStreamingAsync();
                using var content = download.Value.Content;

                var meta = await JsonSerializer.DeserializeAsync<Meta>(content)
                           ?? throw new InvalidOperationException("Ugyldigt metadataformat");

                var imageBlob = _container.GetBlobClient(meta.blobName);
                var sas = await GetReadSasAsync(imageBlob, TimeSpan.FromDays(7));

                results.Add(new ImageItem
                {
                    Id = meta.id,
                    Description = meta.description,
                    UploadedAt = meta.uploadedAt,
                    ImageUrl = sas.ToString()
                });
            }

            return results.OrderByDescending(r => r.UploadedAt).ToList();
        }


        private static string GetSafeExtension(string filename, string contentType)
        {
            var ext = Path.GetExtension(filename);
            if (string.IsNullOrEmpty(ext))
            {
                ext = contentType switch
                {
                    "image/jpeg" => ".jpg",
                    "image/png" => ".png",
                    "image/gif" => ".gif",
                    "image/webp" => ".webp",
                    _ => ".bin"
                };
            }
            return ext.ToLowerInvariant();
        }

        private async Task<Uri> GetReadSasAsync(BlobClient blob, TimeSpan lifetime)
        {
            var expiresOn = DateTimeOffset.UtcNow.Add(lifetime);

            if (blob.CanGenerateSasUri)
            {
                var builder = new BlobSasBuilder(BlobSasPermissions.Read, expiresOn)
                {
                    BlobContainerName = blob.BlobContainerName,
                    BlobName = blob.Name
                };
                return blob.GenerateSasUri(builder);
            }

            var key = await _serviceClient.GetUserDelegationKeyAsync(DateTimeOffset.UtcNow.AddMinutes(-5), expiresOn);
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = blob.BlobContainerName,
                BlobName = blob.Name,
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                ExpiresOn = expiresOn
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sas = sasBuilder.ToSasQueryParameters(key, _accountName);
            var uriBuilder = new UriBuilder(blob.Uri)
            {
                Query = sas.ToString()
            };
            return uriBuilder.Uri;
        }
    }
}