namespace Application.DTOs;

public sealed record GenerateUploadSasResult(string UploadUrl, DateTimeOffset ExpiresAt);
