using System.Net;

namespace Sakin.Ingest.Parsers.Utilities;

public static class IpParser
{
    public static bool TryValidateAndNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        if (IPAddress.TryParse(input.Trim(), out var ipAddress))
        {
            normalized = ipAddress.ToString();
            return true;
        }

        return false;
    }

    public static bool IsPrivate(string ip)
    {
        if (!IPAddress.TryParse(ip, out var address))
            return false;

        var bytes = address.GetAddressBytes();

        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
            (bytes[0] == 10 ||
             (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
             (bytes[0] == 192 && bytes[1] == 168));
    }
}
