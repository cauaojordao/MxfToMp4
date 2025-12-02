using System.Runtime.CompilerServices;
using Application.Interfaces;

namespace Infrastructure.Services.SSE;

public sealed class SseEventSubscriber : IEventSubscriber
{
    private readonly SseChannelHub _hub;

    public SseEventSubscriber(SseChannelHub hub)
    {
        _hub = hub;
    }

    public async IAsyncEnumerable<string> SubscribeAsync(string topic, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var reader = _hub.Subscribe(topic);

        while (!ct.IsCancellationRequested &&
               await reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            if (ct.IsCancellationRequested)
                yield break;

            while (reader.TryRead(out var message))
            {
                yield return message.Data;
            }
        }
    }
}

