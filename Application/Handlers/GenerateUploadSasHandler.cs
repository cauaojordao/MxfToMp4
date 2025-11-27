using Application.DTOs;
using Application.Interfaces;
using Domain.Enums;
using Domain.Repositories;

namespace Application.Commands.GenerateUploadSas;

public sealed class GenerateUploadSasHandler
    : ICommandHandler<GenerateUploadSasCommand, GenerateUploadSasResult>
{
    private readonly IMxfProcessRepository _repository;
    private readonly IBlobService _blob;
    private readonly DateTimeOffset _sasExpiry = DateTimeOffset.UtcNow.AddMinutes(30);

    public GenerateUploadSasHandler(
        IMxfProcessRepository repository,
        IBlobService blob)
    {
        _repository = repository;
        _blob = blob;
    }

    public async Task<GenerateUploadSasResult> HandleAsync(GenerateUploadSasCommand command, CancellationToken cancellationToken = default)
    {
        var process = await _repository.GetAsync(command.ProcessId, cancellationToken)
            ?? throw new ArgumentException("Not found");

        if (process.Status != ProcessStatus.Uploading)
            throw new InvalidOperationException($"Process {command.ProcessId} does not allow upload in state {process.Status}");

        Uri sas = await _blob.GenerateUploadSasUrlAsync(
            blobPath: process.InputBlobPath,
            expiry: _sasExpiry,
            cancellationToken);

        return new GenerateUploadSasResult(
            UploadUrl: sas.ToString(),
            ExpiresAt: _sasExpiry
        );
    }
}
