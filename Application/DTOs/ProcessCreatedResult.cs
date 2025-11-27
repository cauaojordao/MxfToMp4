namespace Application.DTOs;

public sealed class ProcessCreatedResult
{
    public Guid ProcessId { get; init; }
    public string BlobPath { get; init; } = null!;
    public DateTimeOffset CreatedAt { get; init; }
}