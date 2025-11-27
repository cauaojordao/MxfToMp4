namespace Application.Commands;

public sealed class StartProcessingCommand
{
    public Guid ProcessId { get; init; }
}