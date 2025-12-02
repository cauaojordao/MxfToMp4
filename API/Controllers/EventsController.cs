using System.Text.Json;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Api.Controllers;

[ApiController]
[Route("v1/events")]
public sealed class EventsController : ControllerBase
{
    private readonly IEventSubscriber _subscriber;
    private readonly ILogger<EventsController> _logger;

    public EventsController(IEventSubscriber subscriber, ILogger<EventsController> logger)
    {
        _subscriber = subscriber;
        _logger = logger;
    }

    [HttpGet("process/{id:guid}")]
    public async Task Stream([FromRoute] Guid id, CancellationToken ct)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers[HeaderNames.Connection] = "keep-alive";

        // Primeiro ping para garantir conexão
        await Response.WriteAsync(": connected\n\n", ct);
        await Response.Body.FlushAsync(ct);

        await foreach (var evt in _subscriber.SubscribeAsync($"process:{id}", ct))
        {
            if (ct.IsCancellationRequested)
                break;
            
            var inner = JsonSerializer.Deserialize<object>(evt);
            var json = JsonSerializer.Serialize(inner);

            await Response.WriteAsync("event: update\n", ct);
            await Response.WriteAsync($"data: {json}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }
}