using Application.Commands;
using Application.Interfaces;
using Domain.Repositories;

namespace Application.Handlers;
public class FinishProcessingHandler : ICommandHandler<FinishProcessingCommand, bool>
{
    private readonly IMxfProcessRepository _repo;
    private readonly IEventPublisher _events;

    private const string OutputContainer = "mxf-output";

    public FinishProcessingHandler(IMxfProcessRepository repo, IEventPublisher events)
    {
        _repo = repo;
        _events = events;
    }

    public async Task<bool> HandleAsync(FinishProcessingCommand command, CancellationToken cancellationToken = default)
    {
        var aggregate = await _repo.GetAsync(command.ProcessId, cancellationToken);
        if (aggregate == null) throw new InvalidOperationException("Process not found");

        // set output path (container included)
        var outputFullPath = $"{OutputContainer}/{command.OutputBlobPath}";
        aggregate.MarkProcessingCompleted(outputFullPath);

        await _repo.SaveAsync(aggregate, cancellationToken);

        // publish done with output path
        await _events.PublishAsync(command.ProcessId, new { status = "done", mp4 = outputFullPath }, cancellationToken);

        return true;
    }
}

