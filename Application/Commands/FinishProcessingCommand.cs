namespace Application.Commands;

public sealed class FinishProcessingCommand
{
    public Guid ProcessId { get; init; }
    public string OutputBlobPath { get; init; } = null!;
}