using Application.DTOs;
using Application.Interfaces.Mediator;

namespace Application.Queries;

public sealed class GetStatusQuery : IRequest<StatusDto>
{
    public Guid ProcessId { get; init; }
}