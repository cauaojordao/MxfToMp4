using Application.Commands;
using Application.Interfaces;
using Application.Interfaces.Mediator;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Domain.Entities;
using Domain.Repositories;

namespace Worker;

public sealed class MxfWorkerHostedService : BackgroundService
{
    private readonly ILogger<MxfWorkerHostedService> _logger;
    private readonly QueueServiceClient _queueClientRoot;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FfmpegRunner _ffmpeg;
    private readonly IConfiguration _config;

    private readonly string _queueName;
    private readonly string _poisonName;
    private readonly int _maxRetries;
    private readonly int _visibilityTimeout;
    private readonly string _tempFolder;
    private readonly int _pollInterval;

    public MxfWorkerHostedService(
        ILogger<MxfWorkerHostedService> logger,
        QueueServiceClient queueClient,
        IServiceScopeFactory scopeFactory,
        FfmpegRunner ffmpeg,
        IConfiguration config)
    {
        _logger = logger;
        _queueClientRoot = queueClient;
        _scopeFactory = scopeFactory;
        _ffmpeg = ffmpeg;
        _config = config;

        _queueName = config["Queue:ProcessingQueueName"]!;
        _poisonName = config["Queue:PoisonQueueName"]!;
        _maxRetries = config.GetValue("Queue:MaxDeliveryCount", 5);
        _visibilityTimeout = config.GetValue("Worker:VisibilityTimeoutSeconds", 300);
        _tempFolder = config.GetValue("Worker:TempFolder", Path.GetTempPath())!;
        _pollInterval = config.GetValue("Worker:PollIntervalSeconds", 5);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_tempFolder);

        var queue = _queueClientRoot.GetQueueClient(_queueName);
        var poison = _queueClientRoot.GetQueueClient(_poisonName);

        await queue.CreateIfNotExistsAsync(cancellationToken: ct);
        await poison.CreateIfNotExistsAsync(cancellationToken: ct);

        _logger.LogInformation("Worker started. Queue={Queue}", _queueName);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var msgResponse = await queue.ReceiveMessagesAsync(
                    1,
                    TimeSpan.FromSeconds(_visibilityTimeout),
                    ct);

                var msg = msgResponse.Value.FirstOrDefault();

                if (msg == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_pollInterval), ct);
                    continue;
                }

                await ProcessAsync(queue, poison, msg, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker loop failure");
                await Task.Delay(2000, ct);
            }
        }
    }

    private async Task ProcessAsync(
        QueueClient queue,
        QueueClient poison,
        QueueMessage msg,
        CancellationToken ct)
    {
        
        Directory.CreateDirectory(_tempFolder);
        
        _logger.LogInformation("Dequeued {Id}: {Body}", msg.MessageId, msg.MessageText);

        if (!Guid.TryParse(msg.MessageText, out var processId))
        {
            await MoveToPoison(queue, poison, msg, ct);
            return;
        }

        var messageId = msg.MessageId;
        var popReceipt = msg.PopReceipt;

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IMxfProcessRepository>();
        var blob = scope.ServiceProvider.GetRequiredService<IBlobService>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        string? tmpFolder = null;
        string? input = null;

        try
        {
            await mediator.Send(new StartProcessingCommand { ProcessId = processId }, ct);

            var aggregate = await repo.GetAsync(processId, ct)
                            ?? throw new InvalidOperationException("Process not found");

            input = aggregate.Path;
            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
                throw new FileNotFoundException($"Input file not found: {input}");

            tmpFolder = Path.Combine(_tempFolder, $"{processId}");
            Directory.CreateDirectory(tmpFolder);

            var progress = new Progress<double>(async _ =>
            {
                try
                {
                    var updateResp = await queue.UpdateMessageAsync(
                        messageId,
                        popReceipt,
                        msg.MessageText,
                        TimeSpan.FromSeconds(_visibilityTimeout),
                        ct);

                    popReceipt = updateResp.Value.PopReceipt;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to extend visibility for message {MessageId}", messageId);
                }
            });

            var outputFile = Path.Combine(_tempFolder, $"{processId}.mp4");

            await _ffmpeg.RunAsync(input, outputFile, progress, ct);

            using (var fs = File.OpenRead(outputFile))
            {
                await blob.UploadAsync($"{processId}.mp4", fs, ct);
            }

            await mediator.Send(new FinishProcessingCommand
            {
                ProcessId = processId,
                OutputBlobPath = $"{processId}.mp4"
            }, ct);

            try
            {
                await queue.DeleteMessageAsync(messageId, popReceipt, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete message {MessageId} (it may already be gone).", messageId);
            }

            _logger.LogInformation("Process {Pid} COMPLETED", processId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failure while processing {Pid}", processId);

            await mediator.Send(new ReportErrorCommand
            {
                ProcessId = processId,
                Error = ex.Message
            }, ct);

            if (msg.DequeueCount >= _maxRetries)
                await MoveToPoison(queue, poison, msg, ct);
            else
            {
                try
                {
                    var upd = await queue.UpdateMessageAsync(
                        messageId,
                        popReceipt,
                        msg.MessageText,
                        TimeSpan.FromSeconds(30),
                        ct);

                    popReceipt = upd.Value.PopReceipt;
                }
                catch (Exception warnEx)
                {
                    _logger.LogWarning(warnEx, "Failed to set short visibility for message {MessageId}", messageId);
                }
            }
        }
        finally
        {
            try
            {
                if (input != null && File.Exists(input))
                    File.Delete(input);

                if (tmpFolder != null && Directory.Exists(tmpFolder))
                    Directory.Delete(tmpFolder, true);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Cleanup failed for process {ProcessId}", processId);
            }
        }
    }


    private async Task MoveToPoison(
        QueueClient queue,
        QueueClient poison,
        QueueMessage msg,
        CancellationToken ct)
    {
        await poison.SendMessageAsync(msg.MessageText, cancellationToken: ct);
        await queue.DeleteMessageAsync(msg.MessageId, msg.PopReceipt, ct);

        _logger.LogWarning("Message {Id} moved to poison queue", msg.MessageId);
    }
}