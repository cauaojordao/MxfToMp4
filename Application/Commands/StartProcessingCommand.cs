using Application.Interfaces.Mediator;

namespace Application.Commands;

public sealed class StartProcessingCommand : IRequest<bool>
{
    public Guid ProcessId { get; init; }
}