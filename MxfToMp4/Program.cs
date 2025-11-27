using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http.Features;
using System.Threading.Channels;
using MxfToMp4;

var builder = WebApplication.CreateBuilder(args);

// Allow very large multipart sections if used for metadata
builder.Services.Configure<FormOptions>(opts => {
    opts.MultipartBodyLengthLimit = long.MaxValue;
});

// DI
builder.Services.AddSingleton<IProcessRepository, InMemoryProcessRepository>();
builder.Services.AddSingleton<Channel<ProcessJob>>(sp => Channel.CreateUnbounded<ProcessJob>());
builder.Services.AddSingleton<INotificationService, InMemoryNotificationService>();
builder.Services.AddHostedService<AzureConversionBackgroundService>();

// Storage service (Azure)
builder.Services.AddSingleton<IAzureStorageService, AzureBlobStorageService>();

// FFmpeg runner
builder.Services.AddSingleton<IFFmpegRunner, FFmpegRunner>();

builder.Services.AddLogging();

var app = builder.Build();

app.MapPost("/v1/mxf/initiate", async (HttpRequest request,
                                       IAzureStorageService storage,
                                       IProcessRepository repo,
                                       INotificationService notifier) =>
{
    // Accept metadata via JSON body or form. For simplicity assume JSON.
    var body = await request.ReadFromJsonAsync<InitiateUploadRequest>();
    if (body == null) return Results.BadRequest("Invalid body");

    // Validate extension
    if (!body.FileName.EndsWith(".mxf", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Only .mxf allowed" });

    var processId = Guid.NewGuid();
    var uploadBlobName = $"uploads/{processId}/{Path.GetFileName(body.FileName)}";

    // Create process record
    var rec = new ProcessRecord {
        Id = processId,
        OriginalFileName = body.FileName,
        CreatedAt = DateTime.UtcNow,
        Status = ProcessStatus.Uploading,
        UploadBlobName = uploadBlobName
    };
    repo.Add(rec);

    // Generate SAS for the blob with permissions to Put Block / Put Block List and Write (Create/Add)
    // Suggest short expiry (e.g. 1 hour) for upload
    var uploadSas = storage.GenerateBlobSasUri(uploadBlobName, permissions: BlobSasPermissions.Write | BlobSasPermissions.Create | BlobSasPermissions.Add, expiresIn: TimeSpan.FromHours(1));

    // Notify SSE subscribers (if any)
    notifier.Publish(processId, new StatusMessage { Status = rec.Status.ToString().ToLower(), Message = "upload sas created" });

    return Results.Ok(new {
        processId = processId,
        uploadUrl = uploadSas.ToString(),
        suggestedBlockSize = 8 * 1024 * 1024 // 8MB
    });
});

app.MapPost("/v1/mxf/commit", async (CommitRequest req, IAzureStorageService storage, IProcessRepository repo, Channel<ProcessJob> queue, INotificationService notifier) =>
{
    var rec = repo.Get(req.ProcessId);
    if (rec == null) return Results.NotFound();

    // Optional: validate that blob exists
    // Commit block list: backend can commit using the block IDs sent by frontend
    await storage.CommitBlockListAsync(rec.UploadBlobName, req.BlockIds);

    rec.Status = ProcessStatus.Committed;
    repo.Update(rec);

    notifier.Publish(rec.Id, new StatusMessage { Status = rec.Status.ToString().ToLower(), Message = "upload committed" });

    // Enqueue for processing
    var job = new ProcessJob { ProcessId = rec.Id };
    await queue.Writer.WriteAsync(job);

    return Results.Accepted(new { processId = rec.Id, status = rec.Status.ToString().ToLower() });
});

app.MapGet("/v1/mxf/{id:guid}", (Guid id, IProcessRepository repo, IAzureStorageService storage) =>
{
    var rec = repo.Get(id);
    if (rec == null) return Results.NotFound();

    string? mp4Url = null;
    if (rec.Status == ProcessStatus.Done && rec.ProcessedBlobName != null) {
        mp4Url = storage.GenerateBlobSasUri(rec.ProcessedBlobName, BlobSasPermissions.Read, TimeSpan.FromMinutes(10)).ToString();
    }

    return Results.Ok(new {
        status = rec.Status.ToString().ToLower(),
        originalFileName = rec.OriginalFileName,
        createdAt = rec.CreatedAt,
        mp4Url
    });
});

// SSE endpoint
app.MapGet("/v1/mxf/stream/{id:guid}", async (Guid id, INotificationService notifier, HttpResponse res, CancellationToken ct) =>
{
    res.Headers.Add("Content-Type", "text/event-stream");
    res.Headers.CacheControl = "no-cache";
    var channel = notifier.Subscribe(id);

    try {
        await foreach (var msg in channel.ReadAllAsync(ct)) {
            var data = System.Text.Json.JsonSerializer.Serialize(msg);
            await res.WriteAsync($"data: {data}\n\n", ct);
            await res.Body.FlushAsync(ct);
        }
    } catch (OperationCanceledException) {
        // client disconnected
    } finally {
        notifier.Unsubscribe(id, channel);
    }
});

app.Run();


// DTOs
public record InitiateUploadRequest(string FileName);
public record CommitRequest(Guid ProcessId, string[] BlockIds);
