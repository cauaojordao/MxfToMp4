using Application.Commands;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Repositories;
using System.Text.RegularExpressions;

namespace Application.Handlers;

public sealed class CreateProcessHandler : ICommandHandler<CreateProcessCommand, ProcessCreatedResult>
{
    private readonly IMxfProcessRepository _repo;
    private readonly IEventPublisher _events;
    private const string InputContainer = "mxf-input";

    public CreateProcessHandler(
        IMxfProcessRepository repo,
        IEventPublisher events)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<ProcessCreatedResult> HandleAsync(CreateProcessCommand command, CancellationToken cancellationToken = default)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
        if (string.IsNullOrWhiteSpace(command.FileName)) throw new ArgumentException("fileName is required", nameof(command.FileName));
        if (!command.FileName.EndsWith(".mxf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only files with .mxf extension are allowed", nameof(command.FileName));

        var id = Guid.NewGuid();
        var safeName = SanitizeFileName(command.FileName);
        var relative = $"{id}/{safeName}"; // relative path inside container
        var fullBlobPath = $"{InputContainer}/{relative}"; // stored in aggregate as container/relative

        // Build domain aggregate
        var aggregate = MxfProcess.Create(id, fullBlobPath);

        // Persist aggregate (repository implementation must be atomic)
        await _repo.SaveAsync(aggregate, cancellationToken);

        // Publish initial event so listeners (SSE, monitoring) know upload started
        // payload kept small and JSON-serializable
        await _events.PublishAsync(id, new { status = "uploading", blob = aggregate.InputBlobPath, createdAt = aggregate.CreatedAt }, cancellationToken);


        return new ProcessCreatedResult
        {
            ProcessId = id,
            BlobPath = fullBlobPath,
            CreatedAt = aggregate.CreatedAt
        };
    }

    private static string SanitizeFileName(string name)
    {
        // remove path and replace unsafe chars. Keep extension.
        var file = Path.GetFileName(name) ?? name;
        // replace anything not alnum, dash, underscore or dot
        var sanitized = Regex.Replace(file, @"[^\w\-.]", "_");
        // ensure not too long (e.g., 200 chars)
        return sanitized.Length <= 200 ? sanitized : sanitized.Substring(0, 200);
    }
}
