namespace Application.Interfaces;

public interface IEventSubscriber
{
    IAsyncEnumerable<string> SubscribeAsync(string topic, CancellationToken ct = default);
}