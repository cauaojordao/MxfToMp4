using System.Text.Json;
using Domain.Entities;
using Domain.Repositories;
using StackExchange.Redis;

namespace Infrastructure.Services.Redis;

public sealed class RedisMxfProcessRepository : IMxfProcessRepository
{
    private readonly IDatabase _db;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        IncludeFields = false,
        PropertyNameCaseInsensitive = true
    };

    public RedisMxfProcessRepository(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<MxfProcess?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var json = await _db.StringGetAsync(id.ToString());

        if (!json.HasValue)
            return null;

        var process = JsonSerializer.Deserialize<MxfProcess>(json!, JsonOptions);
        return process;
    }

    public async Task SaveAsync(MxfProcess process, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(process, JsonOptions);

        await _db.StringSetAsync(process.Id.ToString(), json, when: When.Always);
    }
}