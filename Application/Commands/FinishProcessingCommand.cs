using Application.Interfaces.Mediator;

namespace Application.Commands;

public sealed class FinishProcessingCommand : IRequest<bool>
{
    public Guid ProcessId { get; init; }
    public string OutputBlobPath { get; init; } = null!;
}