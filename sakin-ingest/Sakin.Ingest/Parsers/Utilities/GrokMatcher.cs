using System.Text.RegularExpressions;

namespace Sakin.Ingest.Parsers.Utilities;

public class GrokMatcher
{
    private readonly Regex _pattern;

    public GrokMatcher(string pattern)
    {
        _pattern = RegexCache.GetOrCreate(pattern);
    }

    public bool TryMatch(string input, out Dictionary<string, string> groups)
    {
        groups = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var match = _pattern.Match(input);
        if (!match.Success)
            return false;

        foreach (var groupName in _pattern.GetGroupNames())
        {
            if (int.TryParse(groupName, out _))
                continue;

            var value = match.Groups[groupName].Value;
            if (!string.IsNullOrEmpty(value))
            {
                groups[groupName] = value;
            }
        }

        return groups.Count > 0;
    }

    public Dictionary<string, string>? Match(string input)
    {
        TryMatch(input, out var groups);
        return groups.Count > 0 ? groups : null;
    }
}
