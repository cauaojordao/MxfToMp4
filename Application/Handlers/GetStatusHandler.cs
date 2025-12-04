// Application/Handlers/GetStatusHandler.cs
using Application.DTOs;
using Application.Interfaces;
using Application.Interfaces.Mediator;
using Application.Queries;
using Domain.Enums;
using Domain.Repositories;

namespace Application.Handlers;

public class GetStatusHandler : IRequestHandler<GetStatusQuery, StatusDto>
{
    private readonly IMxfProcessRepository _repo;
    private readonly IBlobService _blob;

    public GetStatusHandler(IMxfProcessRepository repo, IBlobService blob)
    {
        _repo = repo;
        _blob = blob;
    }

    public async Task<StatusDto> Handle(GetStatusQuery query, CancellationToken cancellationToken = default)
    {
        var aggregate = await _repo.GetAsync(query.ProcessId, cancellationToken);
        if (aggregate == null) throw new KeyNotFoundException("Process not found");

        string? readUrl = null;

        if (aggregate.Status == ProcessStatus.Completed)
        {
            var sas = await _blob.GenerateReadSasAsync(aggregate.OutputBlobPath);
            readUrl = sas.ToString();
        }

        return new StatusDto
        {
            ProcessId = aggregate.Id,
            Status = aggregate.Status.ToString().ToLowerInvariant(),
            ErrorMessage = aggregate.ErrorMessage,
            ReadUrl = readUrl,
            InputBlobPath = aggregate.Path,
            FileSize = aggregate.FileSize == 0 ? null : aggregate.FileSize,
            CreatedAt = aggregate.CreatedAt
        };
    }

}
