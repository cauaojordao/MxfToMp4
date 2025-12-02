using System.IO;
using Application.DTOs;

namespace Application.Interfaces;

public interface IBlobService
{
    Task UploadAsync(string blobPath, Stream data, CancellationToken ct);
    Task<Uri> GenerateReadSasAsync(string blobName);
}