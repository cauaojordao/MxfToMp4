namespace Application.Commands;

public sealed class GenerateUploadSasCommand
{
    public string FileName { get; init; } = null!;
    public string ContentType { get; init; } = null!;
    public long? ExpectedSize { get; init; }
    public TimeSpan SasValidity { get; init; } = TimeSpan.FromHours(4);
}