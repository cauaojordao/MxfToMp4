namespace Application.DTOs;

public sealed class StatusDto
{
    public Guid ProcessId { get; init; }
    public string Status { get; init; } = null!;
    public string? ErrorMessage { get; init; }
    public string? ReadUrl { get; init; }
    public string InputBlobPath { get; init; } = null!;
    public long? FileSize { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}