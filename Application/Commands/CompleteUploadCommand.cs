namespace Application.Commands;

public sealed class CompleteUploadCommand
{
    public Guid ProcessId { get; init; }
    public string BlobPath { get; init; } = null!;
    public long ContentLength { get; init; }
    public string ETag { get; init; } = null!;
}