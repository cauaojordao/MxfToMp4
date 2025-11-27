using Application.Commands;
using Application.Interfaces;
using Domain.Repositories;

namespace Application.Handlers;

public class CompleteUploadHandler : ICommandHandler<CompleteUploadCommand, bool>
{
    private readonly IMxfProcessRepository _repo;
    private readonly IBlobService _blob;
    private readonly IQueueService _queue;
    private readonly IEventPublisher _events;
    private const string InputContainer = "mxf-input";
    private const string QueueName = "process:queue";

    public CompleteUploadHandler(IMxfProcessRepository repo, IBlobService blob, IQueueService queue, IEventPublisher events)
    {
        _repo = repo;
        _blob = blob;
        _queue = queue;
        _events = events;
    }

    public async Task<bool> HandleAsync(CompleteUploadCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ProcessId == Guid.Empty) throw new ArgumentException("processId required");

        // validate blob
        var blobPath = command.BlobPath;
        var (exists, length, etag) = await _blob.GetBlobPropertiesAsync(blobPath, cancellationToken);

        if (!exists)
            throw new InvalidOperationException("Blob not found");

        // load aggregate
        var aggregate = await _repo.GetAsync(command.ProcessId, cancellationToken);
        if (aggregate == null) throw new InvalidOperationException("Process not found");

        // mark upload complete (this will set size + timestamps + set to queued)
        aggregate.MarkUploadCompleted(length ?? command.ContentLength);

        // persist
        await _repo.SaveAsync(aggregate, cancellationToken);

        // enqueue for processing
        await _queue.EnqueueAsync(QueueName, command.ProcessId, cancellationToken);

        // publish event
        await _events.PublishAsync<object>(command.ProcessId, new { status = "queued", blob = aggregate.InputBlobPath }, cancellationToken);

        return true;
    }
}
