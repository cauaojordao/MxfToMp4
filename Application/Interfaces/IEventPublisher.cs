namespace Application.Interfaces;

public interface IEventPublisher
{
    /// <summary>
    /// Publish a lightweight JSON event for subscribers (SSE / PubSub).
    /// </summary>
    Task PublishAsync<T>(Guid processId, T data, CancellationToken ct = default);
}