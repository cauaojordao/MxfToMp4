using Application.Commands;
using Application.Interfaces;
using Domain.Repositories;

namespace Application.Handlers;

public class StartProcessingHandler : ICommandHandler<StartProcessingCommand, bool>
{
    private readonly IMxfProcessRepository _repo;
    private readonly IEventPublisher _events;

    public StartProcessingHandler(IMxfProcessRepository repo, IEventPublisher events)
    {
        _repo = repo;
        _events = events;
    }

    public async Task<bool> HandleAsync(StartProcessingCommand command, CancellationToken cancellationToken = default)
    {
        var aggregate = await _repo.GetAsync(command.ProcessId);
        if (aggregate == null) throw new InvalidOperationException("Process not found");

        aggregate.MarkProcessingStarted();
        await _repo.SaveAsync(aggregate);

        await _events.PublishEventAsync(command.ProcessId, new { status = "processing" }, cancellationToken);

        return true;
    }
}
