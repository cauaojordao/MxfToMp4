namespace Infrastructure.Services.Storage;

public sealed class AzureBlobOptions
{
    public string AccountName { get; set; } = "";
    public string AccountKey { get; set; } = "";
    public string Container { get; set; } = "";
}