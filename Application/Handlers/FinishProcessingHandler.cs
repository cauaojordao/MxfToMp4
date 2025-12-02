using Application.Commands;
using Application.Interfaces;
using Application.Interfaces.Mediator;
using Domain.Repositories;

namespace Application.Handlers;

public class FinishProcessingHandler : IRequestHandler<FinishProcessingCommand, bool>
{
    private readonly IMxfProcessRepository _repo;
    private readonly IEventPublisher _events;

    public FinishProcessingHandler(IMxfProcessRepository repo, IEventPublisher events)
    {
        _repo = repo;
        _events = events;
    }

    public async Task<bool> Handle(FinishProcessingCommand command, CancellationToken cancellationToken = default)
    {
        var aggregate = await _repo.GetAsync(command.ProcessId, cancellationToken);
        if (aggregate == null)
            throw new InvalidOperationException("Process not found");

        // Apenas registra o caminho do output (informado pelo Worker)
        aggregate.MarkProcessingCompleted(command.OutputBlobPath);

        await _repo.SaveAsync(aggregate, cancellationToken);

        await _events.PublishAsync(
            command.ProcessId,
            new { status = "done", output = command.OutputBlobPath },
            cancellationToken
        );

        return true;
    }
}