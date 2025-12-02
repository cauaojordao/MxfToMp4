using Application.DTOs;
using Application.Interfaces.Mediator;

namespace Application.Commands;

public sealed class StartUploadCommand : IRequest<StartUploadResult>
{
    public Guid Id { get; init; }
    public string Path { get; init; } = String.Empty;
    public FileStream FileStream { get; init; } = null!;
}
