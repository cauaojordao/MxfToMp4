using Application.Interfaces;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Queue;

public sealed class AzureQueueService : IQueueService
{
    private readonly QueueServiceClient _queueServiceClient;
    private readonly ILogger<AzureQueueService> _logger;

    public AzureQueueService(
        QueueServiceClient queueServiceClient,
        ILogger<AzureQueueService> logger)
    {
        _queueServiceClient = queueServiceClient;
        _logger = logger;
    }

    public async Task EnqueueAsync(string queueName, Guid processId, CancellationToken ct)
    {
        try
        {
            var normalizedQueue = queueName.ToLowerInvariant();

            var queueClient = _queueServiceClient.GetQueueClient(normalizedQueue);

            await queueClient.CreateIfNotExistsAsync(cancellationToken: ct);

            var message = processId.ToString();

            await queueClient.SendMessageAsync(message, cancellationToken: ct);

            _logger.LogInformation("Processo {ProcessId} enviado para fila {Queue}", processId, normalizedQueue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar processo {ProcessId} para fila {Queue}", processId, queueName);
            throw;
        }
    }
}