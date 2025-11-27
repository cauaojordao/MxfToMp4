namespace Application.Commands;

public sealed class ReportErrorCommand
{
    public Guid ProcessId { get; init; }
    public string Error { get; init; } = null!;
}