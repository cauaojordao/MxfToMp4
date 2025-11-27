using System.Text.RegularExpressions;
using Application.Commands;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Repositories;

namespace Application.Handlers;

public class GenerateUploadSasHandler : ICommandHandler<GenerateUploadSasCommand, ProcessCreatedResult>
{
    private readonly IMxfProcessRepository _repo;
    private readonly IBlobService _blob;
    private readonly IQueueService _queue;
    private readonly IEventPublisher _events;
    private const string InputContainer = "mxf-input";

    public GenerateUploadSasHandler(IMxfProcessRepository repo, IBlobService blob, IQueueService queue, IEventPublisher events)
    {
        _repo = repo;
        _blob = blob;
        _queue = queue;
        _events = events;
    }

    public async Task<ProcessCreatedResult> HandleAsync(GenerateUploadSasCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.FileName)) throw new ArgumentException("fileName required");
        if (!command.FileName.EndsWith(".mxf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only .mxf allowed", nameof(command.FileName));

        var id = Guid.NewGuid();
        var blobPath = $"{id}/{SanitizeFileName(command.FileName)}";

        // generate SAS for upload
        Uri sasUri = await _blob.GenerateUploadSasUrlAsync(InputContainer, blobPath, command.SasValidity, cancellationToken);
        var expiresAt = DateTimeOffset.UtcNow.Add(command.SasValidity);

        // create domain aggregate and persist
        var aggregate = MxfProcess.Create(id,$"{InputContainer}/{blobPath}");
        // note: MarkUploadCompleted will be called in CompleteUploadCommand handler
        await _repo.SaveAsync(aggregate);

        // publish initial event (uploading)
        await _events.PublishEventAsync(id, new { status = "uploading", blob = aggregate.InputBlobPath }, cancellationToken);

        return new ProcessCreatedResult
        {
            ProcessId = id,
            UploadUrl = sasUri,
            BlobPath = aggregate.InputBlobPath,
            ExpiresAt = expiresAt.UtcDateTime
        };
    }
    private static string SanitizeFileName(string name) => Regex.Replace(name, @"[^\w\-.]", "_");
}
