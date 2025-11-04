namespace Sakin.Correlation.Models;

public record CorrelationState
{
    public int EventCount { get; init; }

    public List<Guid> EventIds { get; init; } = new();
}
