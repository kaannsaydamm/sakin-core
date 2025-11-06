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
    public class AbuseIpDbProvider : IThreatIntelProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AbuseIpDbProvider> _logger;
        private readonly ThreatIntelProviderOptions? _providerOptions;

        public AbuseIpDbProvider(
            HttpClient httpClient,
            IOptions<ThreatIntelOptions> options,
            ILogger<AbuseIpDbProvider> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _providerOptions = options.Value.Providers
                .FirstOrDefault(p => string.Equals(p.Type, Name, StringComparison.OrdinalIgnoreCase));
        }

        public string Name => "AbuseIPDB";

        public bool Supports(ThreatIntelIndicatorType type) => type is ThreatIntelIndicatorType.Ipv4 or ThreatIntelIndicatorType.Ipv6;

        public async Task<ThreatIntelScore?> LookupAsync(ThreatIntelLookupRequest request, CancellationToken cancellationToken)
        {
            if (_providerOptions is null || !_providerOptions.Enabled)
            {
                _logger.LogDebug("AbuseIPDB provider is disabled or missing configuration");
                return null;
            }

            if (string.IsNullOrWhiteSpace(_providerOptions.ApiKey))
            {
                _logger.LogWarning("AbuseIPDB provider API key is not configured");
                return null;
            }

            var endpoint = $"check?ipAddress={Uri.EscapeDataString(request.Value)}&maxAgeInDays=30";

            try
            {
                using var response = await _httpClient.GetAsync(endpoint, cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return CreateNotFoundScore();
                }

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new HttpRequestException($"AbuseIPDB request failed with {(int)response.StatusCode}: {body}");
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
                _logger.LogWarning(ex, "AbuseIPDB lookup failed for {Ip}", request.Value);
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

        private ThreatIntelScore ParseResponse(JsonElement root)
        {
            if (!root.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("AbuseIPDB response is missing data element");
            }

            var score = dataElement.TryGetProperty("abuseConfidenceScore", out var scoreElement) && scoreElement.TryGetInt32(out var value)
                ? value
                : 0;

            var totalReports = dataElement.TryGetProperty("totalReports", out var totalReportsElement) && totalReportsElement.TryGetInt32(out var reports)
                ? reports
                : 0;

            DateTimeOffset? lastReported = null;
            if (dataElement.TryGetProperty("lastReportedAt", out var lastReportedElement) && lastReportedElement.ValueKind == JsonValueKind.String)
            {
                var lastReportedValue = lastReportedElement.GetString();
                if (DateTimeOffset.TryParse(lastReportedValue, out var parsed))
                {
                    lastReported = parsed;
                }
            }

            var details = new Dictionary<string, object>
            {
                ["total_reports"] = totalReports
            };

            if (score > 0)
            {
                details["status"] = "suspicious";
            }
            else
            {
                details["status"] = "clean";
            }

            return new ThreatIntelScore
            {
                IsKnownMalicious = score >= 80,
                Score = score,
                MatchingFeeds = new[] { Name },
                LastSeen = lastReported,
                Details = details
            };
        }
    }
}
