using Application.Commands;
using Application.DTOs;
using Application.Interfaces;
using Application.Interfaces.Mediator;
using Domain.Entities;
using Domain.Repositories;

namespace Application.Handlers;

public sealed class StartUploadHandler 
    : IRequestHandler<StartUploadCommand, StartUploadResult>
{
    private readonly IMxfProcessRepository _repo;
    private readonly IQueueService _queue;
    private readonly IEventPublisher _events;

    public StartUploadHandler(
        IMxfProcessRepository repo,
        IQueueService queue,
        IEventPublisher events)
    {
        _repo = repo;
        _queue = queue;
        _events = events;
    }

    public async Task<StartUploadResult> Handle(StartUploadCommand command, CancellationToken ct)
    {
        var processId = command.Id;

        var fi = new FileInfo(command.Path);
        if (!fi.Exists)
            throw new FileNotFoundException("Uploaded file not found on disk", command.Path);

        // cria aggregate
        var aggregate = MxfProcess.Create(processId, command.Path);

        // marca upload como concluído
        aggregate.MarkUploadCompleted(fi.Length);

        await _repo.SaveAsync(aggregate, ct);

        // envia para a fila
        await _queue.EnqueueAsync("process-queue", processId, ct);

        // evento realtime opcional
        await _events.PublishAsync(processId, new { status = "queued" }, ct);

        return new StartUploadResult
        {
            ProcessId = processId,
            CreatedAt = aggregate.CreatedAt
        };
    }
}