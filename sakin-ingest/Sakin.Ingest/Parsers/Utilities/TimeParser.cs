using System.Globalization;

namespace Sakin.Ingest.Parsers.Utilities;

public static class TimeParser
{
    private static readonly string[] Iso8601Formats =
    {
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ss"
    };

    private static readonly string[] Rfc5424Formats =
    {
        "yyyy-MM-ddTHH:mm:ss.FFFFFFZ",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFz",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ssz"
    };

    private static readonly string[] ApacheFormats =
    {
        "dd/MMM/yyyy:HH:mm:ss zzz",
        "dd/MMM/yyyy:HH:mm:ss"
    };

    private static readonly string[] CommonFormats =
    {
        "MMM dd HH:mm:ss",
        "MMM  d HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss"
    };

    public static bool TryParse(string? input, out DateTime result)
    {
        result = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Try ISO 8601
        if (DateTime.TryParseExact(input, Iso8601Formats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var iso8601))
        {
            result = iso8601;
            return true;
        }

        // Try RFC5424
        if (DateTime.TryParseExact(input, Rfc5424Formats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var rfc5424))
        {
            result = rfc5424;
            return true;
        }

        // Try Apache format
        if (DateTime.TryParseExact(input, ApacheFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var apache))
        {
            result = apache.ToUniversalTime();
            return true;
        }

        // Try common formats
        if (DateTime.TryParseExact(input, CommonFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var common))
        {
            result = common.ToUniversalTime();
            return true;
        }

        // Try default parsing
        if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var defaultParse))
        {
            result = defaultParse;
            return true;
        }

        return false;
    }
}
