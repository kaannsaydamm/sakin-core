using Sakin.Common.Models;

namespace Sakin.Ingest.Parsers;

public interface IEventParser
{
    string SourceType { get; }
    Task<NormalizedEvent> ParseAsync(EventEnvelope raw);
}
