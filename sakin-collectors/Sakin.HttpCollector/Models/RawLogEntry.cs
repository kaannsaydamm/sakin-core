namespace Sakin.HttpCollector.Models;

public record RawLogEntry
{
    public required string RawMessage { get; init; }
    public required string SourceIp { get; init; }
    public required string ContentType { get; init; }
    public string? XSourceHeader { get; init; }
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
}
