using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Configuration;
using Sakin.Common.Models;

namespace Sakin.ThreatIntelService.Providers
{
    public class OtxProvider : IThreatIntelProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OtxProvider> _logger;
        private readonly ThreatIntelProviderOptions? _providerOptions;

        public OtxProvider(
            HttpClient httpClient,
            IOptions<ThreatIntelOptions> options,
            ILogger<OtxProvider> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _providerOptions = options.Value.Providers
                .FirstOrDefault(p => string.Equals(p.Type, Name, StringComparison.OrdinalIgnoreCase));
        }

        public string Name => "OTX";

        public bool Supports(ThreatIntelIndicatorType type) => type is ThreatIntelIndicatorType.Ipv4
            or ThreatIntelIndicatorType.Ipv6
            or ThreatIntelIndicatorType.Domain
            or ThreatIntelIndicatorType.Url
            or ThreatIntelIndicatorType.FileHash;

        public async Task<ThreatIntelScore?> LookupAsync(ThreatIntelLookupRequest request, CancellationToken cancellationToken)
        {
            if (_providerOptions is null || !_providerOptions.Enabled)
            {
                _logger.LogDebug("OTX provider is disabled or missing configuration");
                return null;
            }

            if (string.IsNullOrWhiteSpace(_providerOptions.ApiKey))
            {
                _logger.LogWarning("OTX provider API key is not configured");
                return null;
            }

            var indicatorPath = GetIndicatorPath(request);
            if (indicatorPath is null)
            {
                return null;
            }

            try
            {
                using var response = await _httpClient.GetAsync(indicatorPath, cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return CreateNotFoundScore();
                }

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new HttpRequestException($"OTX request failed with {(int)response.StatusCode}: {body}");
                }

                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);

                return ParseResponse(document.RootElement);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OTX lookup failed for {Type} {Value}", request.Type, request.Value);
                throw;
            }
        }

        private static ThreatIntelScore CreateNotFoundScore() => new()
        {
            IsKnownMalicious = false,
            Score = 0,
            MatchingFeeds = Array.Empty<string>(),
            Details = new Dictionary<string, object>
            {
                ["status"] = "not_found"
            }
        };

        private string? GetIndicatorPath(ThreatIntelLookupRequest request)
        {
            var encodedValue = Uri.EscapeDataString(request.Value);

            return request.Type switch
            {
                ThreatIntelIndicatorType.Ipv4 => $"indicators/IPv4/{encodedValue}/general",
                ThreatIntelIndicatorType.Ipv6 => $"indicators/IPv6/{encodedValue}/general",
                ThreatIntelIndicatorType.Domain => $"indicators/domain/{encodedValue}/general",
                ThreatIntelIndicatorType.Url => $"indicators/url/{encodedValue}/general",
                ThreatIntelIndicatorType.FileHash => $"indicators/file/{encodedValue}/general",
                _ => null
            };
        }

        private ThreatIntelScore ParseResponse(JsonElement root)
        {
            var feeds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Name };
            var details = new Dictionary<string, object>();
            var score = 0;
            DateTimeOffset? lastSeen = null;

            if (root.TryGetProperty("pulse_info", out var pulseInfo))
            {
                if (pulseInfo.TryGetProperty("count", out var countElement) && countElement.TryGetInt32(out var count))
                {
                    score = count > 0 ? 90 : 0;
                    details["pulse_count"] = count;

                    if (count == 0)
                    {
                        details["status"] = "clean";
                    }
                }

                if (pulseInfo.TryGetProperty("pulses", out var pulsesElement) && pulsesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var pulse in pulsesElement.EnumerateArray())
                    {
                        if (pulse.TryGetProperty("name", out var nameElement))
                        {
                            var name = nameElement.GetString();
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                feeds.Add(name);
                            }
                        }

                        if (pulse.TryGetProperty("modified", out var modifiedElement) && modifiedElement.ValueKind == JsonValueKind.String)
                        {
                            var modifiedValue = modifiedElement.GetString();
                            if (DateTimeOffset.TryParse(modifiedValue, out var parsed))
                            {
                                if (!lastSeen.HasValue || parsed > lastSeen)
                                {
                                    lastSeen = parsed;
                                }
                            }
                        }
                    }
                }
            }

            var malicious = score >= 80;

            return new ThreatIntelScore
            {
                IsKnownMalicious = malicious,
                Score = score,
                MatchingFeeds = feeds.ToArray(),
                LastSeen = lastSeen,
                Details = details
            };
        }
    }
}
