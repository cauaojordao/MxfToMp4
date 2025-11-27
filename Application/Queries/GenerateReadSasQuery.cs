namespace Application.Queries;

public sealed class GenerateReadSasQuery
{
    public Guid ProcessId { get; init; }
    public TimeSpan ValidFor { get; init; } = TimeSpan.FromMinutes(15);
}