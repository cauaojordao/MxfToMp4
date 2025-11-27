using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

namespace MxfToMp4;

public interface IAzureStorageService {
    Uri GenerateBlobSasUri(string blobName, BlobSasPermissions permissions, TimeSpan expiresIn);
    Task CommitBlockListAsync(string blobName, IEnumerable<string> base64BlockIds);
    Task DownloadToStreamAsync(string blobName, Stream destination, CancellationToken ct = default);
    Task UploadFromStreamAsync(string blobName, Stream content, CancellationToken ct = default);
}

public class AzureBlobStorageService : IAzureStorageService {
    private readonly BlobServiceClient _svc;
    private readonly BlobContainerClient _uploadContainer;
    private readonly BlobContainerClient _processedContainer;
    private readonly string _containerName;
    private readonly StorageSharedKeyCredential _cred;

    public AzureBlobStorageService(IConfiguration cfg) {
        var conn = cfg["AZURE_STORAGE_CONNECTION_STRING"]
                   ?? throw new ArgumentNullException("AZURE_STORAGE_CONNECTION_STRING not set");
        _svc = new BlobServiceClient(conn);
        _containerName = cfg["UPLOAD_CONTAINER"] ?? "my-uploads";
        var processed = cfg["PROCESSED_CONTAINER"] ?? "processed";
        _uploadContainer = _svc.GetBlobContainerClient(_containerName);
        _processedContainer = _svc.GetBlobContainerClient(processed);
        // ensure containers exist (idempotent)
        _uploadContainer.CreateIfNotExists();
        _processedContainer.CreateIfNotExists();

        // get account name/key for SAS generation
        // parse from connection string
        var cs = Azure.Storage.StorageConnectionString.Parse(conn);
        _cred = new StorageSharedKeyCredential(cs.AccountName, cs.AccountKey);
    }

    public Uri GenerateBlobSasUri(string blobName, BlobSasPermissions permissions, TimeSpan expiresIn) {
        var blob = _uploadContainer.GetBlockBlobClient(blobName);
        var sasBuilder = new BlobSasBuilder {
            BlobContainerName = _uploadContainer.Name,
            BlobName = blobName,
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiresIn),
            Resource = "b"
        };
        sasBuilder.SetPermissions(permissions);
        var sas = sasBuilder.ToSasQueryParameters(_cred).ToString();
        var uri = new UriBuilder(blob.Uri) { Query = sas }.Uri;
        return uri;
    }

    public async Task CommitBlockListAsync(string blobName, IEnumerable<string> base64BlockIds) {
        var blockBlob = _uploadContainer.GetBlockBlobClient(blobName);
        // Convert base64 block ids to array of Block objects
        var blocks = base64BlockIds.Select(id => new Azure.Storage.Blobs.Models.Block { Name = id, Size = null });
        // But SDK wants Block list as string IDs:
        await blockBlob.CommitBlockListAsync(base64BlockIds);
    }

    public async Task DownloadToStreamAsync(string blobName, Stream destination, CancellationToken ct = default) {
        var blob = _uploadContainer.GetBlobClient(blobName);
        await blob.DownloadToAsync(destination, cancellationToken: ct);
    }

    public async Task UploadFromStreamAsync(string blobName, Stream content, CancellationToken ct = default) {
        var dest = _processedContainer.GetBlobClient(blobName);
        content.Position = 0;
        await dest.UploadAsync(content, overwrite: true, cancellationToken: ct);
    }

    public Uri GenerateProcessedBlobSas(string blobName, BlobSasPermissions permissions, TimeSpan expiresIn) {
        var blob = _processedContainer.GetBlobClient(blobName);
        var sasBuilder = new BlobSasBuilder {
            BlobContainerName = _processedContainer.Name,
            BlobName = blobName,
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiresIn),
            Resource = "b"
        };
        sasBuilder.SetPermissions(permissions);
        var sas = sasBuilder.ToSasQueryParameters(_cred).ToString();
        return new UriBuilder(blob.Uri) { Query = sas }.Uri;
    }
}