using Application.DTOs;
using Application.Interfaces;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Storage;

public sealed class AzureBlobService : IBlobService
{
    private readonly BlobServiceClient _serviceClient;
    private readonly BlobContainerClient _container;
    private readonly AzureBlobOptions _options;
    private readonly ILogger<AzureBlobService> _logger;

    public AzureBlobService(
        IOptions<AzureBlobOptions> options,
        ILogger<AzureBlobService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var credential = new StorageSharedKeyCredential(
            _options.AccountName,
            _options.AccountKey
        );

        _serviceClient = new BlobServiceClient(
            new Uri($"https://{_options.AccountName}.blob.core.windows.net"),
            credential
        );

        _container = _serviceClient.GetBlobContainerClient(_options.Container);
    }

    public async Task UploadAsync(string blobPath, Stream content, CancellationToken ct)
    {
        try
        {
            var blob = _container.GetBlobClient(blobPath);

            var ext = Path.GetExtension(blobPath).ToLowerInvariant();
            string contentType = ext switch
            {
                ".m3u8" => "application/vnd.apple.mpegurl",
                ".ts" => "video/mp2t",
                ".mp4" => "video/mp4",
                _ => "application/octet-stream"
            };

            var headers = new BlobHttpHeaders
            {
                ContentType = contentType,
                CacheControl = "public, max-age=31536000"
            };

            await blob.UploadAsync(
                content,
                new BlobUploadOptions { HttpHeaders = headers },
                ct
            );

            _logger.LogInformation("Uploaded blob {BlobPath} ({ContentType})", blobPath, contentType);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error uploading blob: {BlobPath}", blobPath);
            throw;
        }
    }
    
    public Task<Uri> GenerateReadSasAsync(string blobName)
    {
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = blobName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sas = sasBuilder.ToSasQueryParameters(
            new StorageSharedKeyCredential(
                _serviceClient.AccountName,
                _options.AccountKey
            )
        );

        return Task.FromResult(new Uri($"{_container.Uri}/{blobName}?{sas}"));
    }
}