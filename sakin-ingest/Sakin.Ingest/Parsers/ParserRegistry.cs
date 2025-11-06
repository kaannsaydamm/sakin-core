namespace Sakin.Ingest.Parsers;

public class ParserRegistry
{
    private readonly Dictionary<string, IEventParser> _parsers = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IEventParser parser)
    {
        if (parser == null)
            throw new ArgumentNullException(nameof(parser));

        _parsers[parser.SourceType] = parser;
    }

    public bool TryGetParser(string sourceType, out IEventParser? parser)
    {
        return _parsers.TryGetValue(sourceType, out parser);
    }

    public IEventParser? GetParser(string sourceType)
    {
        _parsers.TryGetValue(sourceType, out var parser);
        return parser;
    }

    public IEnumerable<string> GetRegisteredSourceTypes()
    {
        return _parsers.Keys;
    }
}
