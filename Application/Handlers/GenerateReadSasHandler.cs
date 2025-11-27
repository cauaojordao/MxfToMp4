// Application/Handlers/GenerateReadSasHandler.cs
using Application.Interfaces;
using Application.Queries;
using Domain.Enums;
using Domain.Repositories;

namespace Application.Handlers;

public class GenerateReadSasHandler : IQueryHandler<GenerateReadSasQuery, Uri>
{
    private readonly IMxfProcessRepository _repo;
    private readonly IBlobService _blob;

    private const string OutputContainer = "mxf-output";

    public GenerateReadSasHandler(IMxfProcessRepository repo, IBlobService blob)
    {
        _repo = repo;
        _blob = blob;
    }

    public async Task<Uri> HandleAsync(GenerateReadSasQuery query, CancellationToken cancellationToken = default)
    {
        var aggregate = await _repo.GetAsync(query.ProcessId, cancellationToken);
        if (aggregate == null) throw new KeyNotFoundException("Process not found");
        if (aggregate.Status != ProcessStatus.Completed || string.IsNullOrWhiteSpace(aggregate.OutputBlobPath))
            throw new InvalidOperationException("Process not completed");

        var blobPath = aggregate.OutputBlobPath;
        var uri = await _blob.GenerateReadSasUrlAsync(blobPath, query.ValidFor, cancellationToken);
        return uri;
    }
}
