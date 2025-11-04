using Sakin.Common.Models;

namespace Sakin.Ingest.Pipelines
{
    public interface IEventPipeline
    {
        Task<NormalizedEvent?> ProcessAsync(RawEvent rawEvent, CancellationToken cancellationToken = default);
    }

    public interface IEventSource
    {
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync(CancellationToken cancellationToken = default);
        event EventHandler<RawEvent>? OnRawEventReceived;
    }

    public interface IEventProcessor
    {
        Task<NormalizedEvent?> ProcessAsync(RawEvent rawEvent, CancellationToken cancellationToken = default);
        string Name { get; }
        int Priority { get; }
    }

    public interface IEventSink
    {
        Task PublishAsync(NormalizedEvent normalizedEvent, CancellationToken cancellationToken = default);
    }

    public record RawEvent
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string Source { get; init; } = string.Empty;
        public string Format { get; init; } = string.Empty;
        public string Data { get; init; } = string.Empty;
        public Dictionary<string, object> Metadata { get; init; } = new();
    }
}