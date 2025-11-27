namespace Application.Interfaces;

public interface IEventPublisher
{
    /// <summary>
    /// Publish a lightweight JSON event for subscribers (SSE / PubSub).
    /// </summary>
    Task PublishEventAsync(Guid processId, object payload, CancellationToken ct);
}