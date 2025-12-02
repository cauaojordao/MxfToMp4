using Application.Interfaces;
using System.Text.Json;

namespace Infrastructure.Services.SSE;

public sealed class SseEventPublisher : IEventPublisher
{
    private readonly SseChannelHub _hub;

    public SseEventPublisher(SseChannelHub hub)
    {
        _hub = hub;
    }

    public async Task PublishAsync<T>(Guid processId, T data, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(data);

        var evt = new SseMessage(
            ProcessId: processId,
            Data: payload,
            Timestamp: DateTimeOffset.UtcNow
        );

        var topic = $"process:{processId}";
        await _hub.PublishAsync(topic, evt, ct);
    }
}
