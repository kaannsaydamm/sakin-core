using Microsoft.Extensions.Logging;
using Sakin.Common.Models;

namespace Sakin.Ingest.Pipelines
{
    public class EventPipeline : IEventPipeline
    {
        private readonly IEnumerable<IEventProcessor> _processors;
        private readonly ILogger<EventPipeline> _logger;

        public EventPipeline(IEnumerable<IEventProcessor> processors, ILogger<EventPipeline> logger)
        {
            _processors = processors.OrderBy(p => p.Priority);
            _logger = logger;
        }

        public async Task<NormalizedEvent?> ProcessAsync(RawEvent rawEvent, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Processing raw event {EventId} from source {Source}", rawEvent.Id, rawEvent.Source);

            foreach (var processor in _processors)
            {
                try
                {
                    var result = await processor.ProcessAsync(rawEvent, cancellationToken);
                    if (result != null)
                    {
                        _logger.LogDebug("Event {EventId} processed successfully by {Processor}", rawEvent.Id, processor.Name);
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Processor {Processor} failed for event {EventId}", processor.Name, rawEvent.Id);
                }
            }

            _logger.LogWarning("No processor could handle event {EventId} from source {Source}", rawEvent.Id, rawEvent.Source);
            return null;
        }
    }
}