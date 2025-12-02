using Application.Interfaces.Mediator;

namespace Application.Commands;

public sealed class ReportErrorCommand : IRequest<bool>
{
    public Guid ProcessId { get; init; }
    public string Error { get; init; } = null!;
}