using Application.Interfaces.Mediator;

namespace Application.Queries;

public sealed class GenerateReadSasQuery : IRequest<Uri>
{
    public Guid ProcessId { get; init; }
    public DateTimeOffset ValidFor { get; init; } = DateTimeOffset.UtcNow.AddMinutes(15);
}