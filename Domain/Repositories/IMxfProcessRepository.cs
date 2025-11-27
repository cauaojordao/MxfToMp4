using Domain.Entities;

namespace Domain.Repositories;

public interface IMxfProcessRepository
{
    Task<MxfProcess?> GetAsync(Guid id, CancellationToken ct);
    Task SaveAsync(MxfProcess process, CancellationToken ct);
}
