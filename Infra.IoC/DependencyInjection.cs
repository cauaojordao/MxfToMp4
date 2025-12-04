using System;
using Application.Commands;
using Application.DTOs;
using Application.Handlers;
using Application.Interfaces;
using Application.Interfaces.Mediator;
using Application.Queries;
using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Domain.Repositories;
using Infrastructure.Services.Mediator;
using Infrastructure.Services.Queue;
using Infrastructure.Services.Redis;
using Infrastructure.Services.SSE;
using Infrastructure.Services.Storage;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infra.IoC;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureIoC(this IServiceCollection s, IConfiguration cfg)
    {
        s.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = ConfigurationOptions.Parse(cfg.GetConnectionString("Redis") ??
                                                    throw new InvalidOperationException(
                                                        "Redis connection string not found."));
            config.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(config);
        });

        s.AddScoped<IMxfProcessRepository, RedisMxfProcessRepository>();

        s.AddScoped<IMediator, Mediator>();
        s.AddScoped<IRequestHandler<GetStatusQuery, StatusDto>, GetStatusHandler>();
        
        s.AddScoped<IRequestHandler<StartUploadCommand, StartUploadResult>, StartUploadHandler>();
        
        s.AddScoped<IRequestHandler<StartProcessingCommand, bool>, StartProcessingHandler>();
        s.AddScoped<IRequestHandler<FinishProcessingCommand, bool>, FinishProcessingHandler>();
        
        s.AddScoped<IRequestHandler<ReportErrorCommand, bool>, ReportErrorHandler>();

        
        s.AddSingleton<SseChannelHub>();
        s.AddSingleton<IEventPublisher, SseEventPublisher>();
        s.AddSingleton<IEventSubscriber, SseEventSubscriber>();

        s.AddSingleton<QueueServiceClient>(provider =>
        {
            var config = provider.GetRequiredService<IConfiguration>();

            var accountName = config["AzureBlob:AccountName"];
            var accountKey = config["AzureBlob:AccountKey"];

            var credential = new StorageSharedKeyCredential(accountName, accountKey);

            return new QueueServiceClient(
                new Uri($"https://{accountName}.queue.core.windows.net"),
                credential
            );
        });

        
        s.AddScoped<IBlobService, AzureBlobService>();
        s.AddScoped<IQueueService, AzureQueueService>();

        return s;
    }
}