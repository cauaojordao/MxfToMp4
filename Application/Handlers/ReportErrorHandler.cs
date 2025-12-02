using Application.Commands;
using Application.Interfaces;
using Application.Interfaces.Mediator;
using Domain.Repositories;

namespace Application.Handlers;

public class ReportErrorHandler : IRequestHandler<ReportErrorCommand, bool>
{
    private readonly IMxfProcessRepository _repo;
    private readonly IEventPublisher _events;

    public ReportErrorHandler(IMxfProcessRepository repo, IEventPublisher events)
    {
        _repo = repo;
        _events = events;
    }

    public async Task<bool> Handle(ReportErrorCommand command, CancellationToken cancellationToken = default)
    {
        var aggregate = await _repo.GetAsync(command.ProcessId, cancellationToken);
        if (aggregate == null) throw new InvalidOperationException("Process not found");

        aggregate.MarkError(command.Error);
        await _repo.SaveAsync(aggregate, cancellationToken);

        await _events.PublishAsync(command.ProcessId, new { status = "error", error = command.Error }, cancellationToken);

        return true;
    }
}
