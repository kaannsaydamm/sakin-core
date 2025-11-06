using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Sakin.Ingest.Parsers.Utilities;

public static class RegexCache
{
    private static readonly ConcurrentDictionary<string, Regex> Cache = new();

    public static Regex GetOrCreate(string pattern)
    {
        return Cache.GetOrAdd(pattern, p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase));
    }

    public static Regex GetOrCreate(string pattern, RegexOptions options)
    {
        var key = $"{pattern}:{(int)options}";
        return Cache.GetOrAdd(key, _ => new Regex(pattern, options | RegexOptions.Compiled));
    }
}
