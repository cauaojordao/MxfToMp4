namespace Application.Interfaces;

public interface IQueueService
{
    /// <summary>Enqueue a process id for background processing.</summary>
    Task EnqueueAsync(string queueName, Guid processId, CancellationToken ct);
}