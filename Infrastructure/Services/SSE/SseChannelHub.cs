using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Infrastructure.Services.SSE;

public sealed class SseChannelHub
{
    private readonly ConcurrentDictionary<string, Channel<SseMessage>> _topics
        = new();

    public ChannelReader<SseMessage> Subscribe(string topic)
    {
        var channel = _topics.GetOrAdd(topic, _ =>
            Channel.CreateUnbounded<SseMessage>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            })
        );

        return channel.Reader;
    }

    public async Task PublishAsync(string topic, SseMessage message, CancellationToken ct)
    {
        if (_topics.TryGetValue(topic, out var channel))
        {
            await channel.Writer.WriteAsync(message, ct);
        }
    }
}

