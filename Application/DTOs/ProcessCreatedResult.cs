namespace Application.DTOs;

public sealed class StartUploadResult
{
    public Guid ProcessId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}