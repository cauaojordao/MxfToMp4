namespace Application.Interfaces;

public interface IBlobService
{
    /// <summary>
    /// Generate a SAS URL for upload (Write/Create). Should be a User Delegation SAS in production.
    /// The returned uri must be a fully qualified URL that supports block blob uploads.
    /// </summary>
    Task<Uri> GenerateUploadSasUrlAsync(string blobPath, DateTimeOffset expiry, CancellationToken ct);

    /// <summary>
    /// Validate blob exists and returns blob metadata (length, etag).
    /// </summary>
    Task<(bool exists, long? length, string? etag)> GetBlobPropertiesAsync(string blobPath, CancellationToken ct);

    /// <summary>
    /// Generate read-only SAS for a blob.
    /// </summary>
    Task<Uri> GenerateReadSasUrlAsync(string blobPath, TimeSpan expiry, CancellationToken ct);
}