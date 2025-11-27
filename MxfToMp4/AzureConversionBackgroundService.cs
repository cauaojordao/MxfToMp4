using System.Threading.Channels;
using MxfToMp4;

public class AzureConversionBackgroundService : BackgroundService {
    private readonly Channel<ProcessJob> _queue;
    private readonly IProcessRepository _repo;
    private readonly IAzureStorageService _storage;
    private readonly INotificationService _notifier;
    private readonly IFFmpegRunner _ffmpeg;
    private readonly ILogger<AzureConversionBackgroundService> _logger;

    public AzureConversionBackgroundService(Channel<ProcessJob> queue, IProcessRepository repo, IAzureStorageService storage,
        INotificationService notifier, IFFmpegRunner ffmpeg, ILogger<AzureConversionBackgroundService> logger) {
        _queue = queue;
        _repo = repo;
        _storage = storage;
        _notifier = notifier;
        _ffmpeg = ffmpeg;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken)) {
            var rec = _repo.Get(job.ProcessId);
            if (rec == null) continue;

            try {
                rec.Status = ProcessStatus.Queued;
                _repo.Update(rec);
                _notifier.Publish(rec.Id, new StatusMessage("queued", "processing queued"));

                // download to temp file streaming
                var tmpDir = Path.Combine(Path.GetTempPath(), "mxf_uploads", rec.Id.ToString());
                Directory.CreateDirectory(tmpDir);
                var inputPath = Path.Combine(tmpDir, "input.mxf");
                await using (var fs = File.Create(inputPath))
                {
                    await _storage.DownloadToStreamAsync(rec.UploadBlobName, fs, stoppingToken);
                }

                rec.Status = ProcessStatus.Processing;
                _repo.Update(rec);
                _notifier.Publish(rec.Id, new StatusMessage("processing", "started ffmpeg"));

                // run ffmpeg to output mp4
                var outFileName = $"{rec.Id}.mp4";
                var outPath = Path.Combine(tmpDir, outFileName);
                await _ffmpeg.ConvertToMp4Async(inputPath, outPath, stoppingToken);

                // upload processed to processed container
                var processedBlobName = $"processed/{rec.Id}/{outFileName}";
                await using (var fsOut = File.OpenRead(outPath)) {
                    await _storage.UploadFromStreamAsync(processedBlobName, fsOut, stoppingToken);
                }

                rec.ProcessedBlobName = processedBlobName;
                rec.Status = ProcessStatus.Done;
                _repo.Update(rec);
                _notifier.Publish(rec.Id, new StatusMessage("done", "processing finished"));

                // cleanup
                try { Directory.Delete(tmpDir, true); } catch { /* ignore */ }
            } catch (Exception ex) {
                rec.Status = ProcessStatus.Error;
                rec.ErrorMessage = ex.Message;
                _repo.Update(rec);
                _notifier.Publish(rec.Id, new StatusMessage("error", ex.Message));
                _logger.LogError(ex, "Processing failed for {id}", job.ProcessId);
            }
        }
    }
}
