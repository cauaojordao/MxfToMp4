using Application.Interfaces;
using Domain.Repositories;
using Infra.Redis;
using Infrastructure.Services.SSE;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Runtime.CompilerServices;

namespace Infra.IoC;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureIoC(this IServiceCollection s)
    {
        s.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis"));
            config.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(config);
        });

        s.AddScoped<IMxfProcessRepository, RedisMxfProcessRepository>();
        s.AddSingleton<SseChannelHub>();
        s.AddScoped<IEventPublisher, SseEventPublisher>();

        return s;
    }
}
