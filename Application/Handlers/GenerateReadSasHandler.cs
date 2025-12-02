// Application/Handlers/GenerateReadSasHandler.cs
using Application.Interfaces;
using Application.Interfaces.Mediator;
using Application.Queries;
using Domain.Enums;
using Domain.Repositories;

namespace Application.Handlers;

public class GenerateReadSasHandler : IRequestHandler<GenerateReadSasQuery, Uri>
{
    private readonly IMxfProcessRepository _repo;
    private readonly IBlobService _blob;

    public GenerateReadSasHandler(IMxfProcessRepository repo, IBlobService blob)
    {
        _repo = repo;
        _blob = blob;
    }

    public async Task<Uri> Handle(GenerateReadSasQuery query, CancellationToken cancellationToken = default)
    {
        var aggregate = await _repo.GetAsync(query.ProcessId, cancellationToken);
        if (aggregate == null) throw new KeyNotFoundException("Process not found");
        if (aggregate.Status != ProcessStatus.Completed)
            throw new InvalidOperationException("Process not completed");

        // Apenas retorna SAS do container
        return await _blob.GenerateReadSasAsync(aggregate.OutputBlobPath);
    }
}
