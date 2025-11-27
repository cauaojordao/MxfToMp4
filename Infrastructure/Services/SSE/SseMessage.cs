namespace Infrastructure.Services.SSE;

public sealed record SseMessage(
    Guid ProcessId,
    string Data,
    DateTimeOffset Timestamp
);