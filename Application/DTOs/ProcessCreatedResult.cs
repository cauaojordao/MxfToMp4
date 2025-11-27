namespace Application.DTOs;

public sealed class ProcessCreatedResult
{
    public Guid ProcessId { get; init; }
    public Uri UploadUrl { get; init; } = null!;
    public string BlobPath { get; init; } = null!;
    public DateTimeOffset ExpiresAt { get; init; }
}