using Domain.Entities;

namespace Domain.Repositories;

public interface IMxfProcessRepository
{
    Task<MxfProcess?> GetAsync(Guid id);
    Task SaveAsync(MxfProcess process);
}
