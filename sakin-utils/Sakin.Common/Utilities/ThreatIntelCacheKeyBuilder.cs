using System;
using Sakin.Common.Models;

namespace Sakin.Common.Utilities
{
    public static class ThreatIntelCacheKeyBuilder
    {
        public static string BuildCacheKey(ThreatIntelIndicatorType type, string value, ThreatIntelHashType? hashType = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(value));
            }

            var normalizedValue = value.Trim().ToLowerInvariant();

            return type switch
            {
                ThreatIntelIndicatorType.FileHash when hashType.HasValue =>
                    $"threatintel:hash:{hashType.Value.ToString().ToLowerInvariant()}:{normalizedValue}",
                ThreatIntelIndicatorType.FileHash =>
                    $"threatintel:hash:{normalizedValue}",
                ThreatIntelIndicatorType.Ipv4 =>
                    $"threatintel:ipv4:{normalizedValue}",
                ThreatIntelIndicatorType.Ipv6 =>
                    $"threatintel:ipv6:{normalizedValue}",
                ThreatIntelIndicatorType.Domain =>
                    $"threatintel:domain:{normalizedValue}",
                ThreatIntelIndicatorType.Url =>
                    $"threatintel:url:{normalizedValue}",
                _ =>
                    $"threatintel:{type.ToString().ToLowerInvariant()}:{normalizedValue}"
            };
        }
    }
}
