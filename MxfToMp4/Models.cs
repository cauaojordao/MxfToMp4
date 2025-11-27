using System.Collections.Concurrent;
using System.Threading.Channels;

namespace MxfToMp4;

public enum JobState
{
    Queued,
    Processing,
    Uploading,
    Completed,
    Error
}

public class ProcessResult
{
    public Guid Id { get; set; }
    public string? OutputUrl { get; set; }
}


public class ProcessJobStatus
{
    public Guid Id { get; set; }

    public JobState State { get; set; } = JobState.Queued;

    public string? Message { get; set; }

    public string? FinalUrl { get; set; }

    public int Progress { get; set; }   // 0–100
}


public class ProcessJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string TempFilePath { get; set; } = default!;

    public string OriginalFileName { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


public enum ProcessStatus { Uploading, Committed, Queued, Processing, Done, Error }

public class ProcessRecord {
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string UploadBlobName { get; set; } = ""; // uploads container path
    public string? ProcessedBlobName { get; set; }
    public ProcessStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? ProcessedSize { get; set; }
}

public interface IProcessRepository {
    void Add(ProcessRecord r);
    ProcessRecord? Get(Guid id);
    void Update(ProcessRecord r);
}
public class InMemoryProcessRepository : IProcessRepository {
    private ConcurrentDictionary<Guid, ProcessRecord> _d = new();
    public void Add(ProcessRecord r) => _d[r.Id] = r;
    public ProcessRecord? Get(Guid id) => _d.TryGetValue(id, out var v) ? v : null;
    public void Update(ProcessRecord r) => _d[r.Id] = r;
}

// Notification service for SSE
public record StatusMessage(string Status, string Message);

public interface INotificationService {
    ChannelReader<StatusMessage> Subscribe(Guid processId);
    void Unsubscribe(Guid processId, ChannelReader<StatusMessage> reader); // optional cleanup
    void Publish(Guid processId, StatusMessage msg);
}

public class InMemoryNotificationService : INotificationService {
    private readonly ConcurrentDictionary<Guid, Channel<StatusMessage>> _channels = new();
    public ChannelReader<StatusMessage> Subscribe(Guid processId) {
        var ch = _channels.GetOrAdd(processId, _ => Channel.CreateUnbounded<StatusMessage>());
        return ch.Reader;
    }
    public void Unsubscribe(Guid processId, ChannelReader<StatusMessage> reader) {
        // best-effort cleanup
        if (_channels.TryGetValue(processId, out var ch) && ch.Reader == reader) {
            // don't delete immediately — other subscribers might exist
        }
    }
    public void Publish(Guid processId, StatusMessage msg) {
        var ch = _channels.GetOrAdd(processId, _ => Channel.CreateUnbounded<StatusMessage>());
        _ = ch.Writer.TryWrite(msg);
    }
}