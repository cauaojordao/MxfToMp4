using Domain.Entities;
using Domain.Repositories;
using StackExchange.Redis;
using System.Text.Json;

namespace Infra.Redis;

public sealed class RedisMxfProcessRepository : IMxfProcessRepository
{
    private readonly IDatabase _db;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
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

        return JsonSerializer.Deserialize<MxfProcess>(json!, _jsonOptions);
    }

    public async Task SaveAsync(MxfProcess process, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(process, _jsonOptions);

        await _db.StringSetAsync(process.Id.ToString(), json, when: When.Always);
    }
}
