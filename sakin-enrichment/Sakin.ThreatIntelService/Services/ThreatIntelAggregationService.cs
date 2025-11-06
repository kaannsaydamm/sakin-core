using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Cache;
using Sakin.Common.Configuration;
using Sakin.Common.Models;
using Sakin.Common.Utilities;
using Sakin.ThreatIntelService.Providers;

namespace Sakin.ThreatIntelService.Services
{
    public class ThreatIntelAggregationService : IThreatIntelService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IEnumerable<IThreatIntelProvider> _providers;
        private readonly IRedisClient _redisClient;
        private readonly IThreatIntelRateLimiter _rateLimiter;
        private readonly ILogger<ThreatIntelAggregationService> _logger;
        private readonly ThreatIntelOptions _options;
        private readonly IReadOnlyDictionary<string, ThreatIntelProviderOptions> _providerOptions;

        public ThreatIntelAggregationService(
            IEnumerable<IThreatIntelProvider> providers,
            IRedisClient redisClient,
            IThreatIntelRateLimiter rateLimiter,
            IOptions<ThreatIntelOptions> options,
            ILogger<ThreatIntelAggregationService> logger)
        {
            _providers = providers;
            _redisClient = redisClient;
            _rateLimiter = rateLimiter;
            _logger = logger;
            _options = options.Value;
            _providerOptions = _options.Providers
                .GroupBy(p => p.Type, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }

        public async Task<ThreatIntelScore> ProcessAsync(ThreatIntelLookupRequest request, CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                return CreateDisabledScore();
            }

            if (string.IsNullOrWhiteSpace(request.Value))
            {
                return CreateInvalidScore("invalid_value");
            }

            var cacheKey = ThreatIntelCacheKeyBuilder.BuildCacheKey(request.Type, request.Value, request.HashType);

            var cached = await TryGetFromCacheAsync(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var providerResults = new List<(string ProviderName, ThreatIntelScore Score)>();

            foreach (var provider in _providers)
            {
                if (!provider.Supports(request.Type))
                {
                    continue;
                }

                if (!_providerOptions.TryGetValue(provider.Name, out var providerOption) || !providerOption.Enabled)
                {
                    continue;
                }

                if (!await _rateLimiter.TryAcquireAsync(provider.Name, providerOption.DailyQuota, cancellationToken))
                {
                    continue;
                }

                try
                {
                    var providerScore = await provider.LookupAsync(request, cancellationToken);
                    if (providerScore != null)
                    {
                        providerResults.Add((provider.Name, NormalizeProviderScore(providerScore, provider.Name)));
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Provider {Provider} HTTP error for {Type} {Value}", provider.Name, request.Type, request.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Provider {Provider} failed for {Type} {Value}", provider.Name, request.Type, request.Value);
                }
            }

            var aggregated = Aggregate(providerResults);

            await CacheAsync(cacheKey, aggregated);

            return aggregated;
        }

        private ThreatIntelScore NormalizeProviderScore(ThreatIntelScore score, string providerName)
        {
            var feeds = (score.MatchingFeeds ?? Array.Empty<string>())
                .Concat(new[] { providerName })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return score with
            {
                MatchingFeeds = feeds,
                Details = score.Details ?? new Dictionary<string, object>()
            };
        }

        private ThreatIntelScore Aggregate(IReadOnlyCollection<(string ProviderName, ThreatIntelScore Score)> providerResults)
        {
            if (providerResults.Count == 0)
            {
                return new ThreatIntelScore
                {
                    IsKnownMalicious = false,
                    Score = 0,
                    MatchingFeeds = Array.Empty<string>(),
                    Details = new Dictionary<string, object>
                    {
                        ["status"] = "not_found"
                    }
                };
            }

            var maxScore = providerResults.Max(r => r.Score.Score);
            var malicious = providerResults.Any(r => r.Score.IsKnownMalicious) || maxScore >= _options.MaliciousScoreThreshold;

            var feeds = providerResults
                .SelectMany(r => r.Score.MatchingFeeds ?? Array.Empty<string>())
                .Concat(providerResults.Select(r => r.ProviderName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var lastSeen = providerResults
                .Where(r => r.Score.LastSeen.HasValue)
                .Select(r => r.Score.LastSeen!.Value)
                .DefaultIfEmpty(DateTimeOffset.MinValue)
                .Max();

            var details = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["status"] = malicious ? "malicious" : "clean"
            };

            foreach (var (providerName, score) in providerResults)
            {
                details[providerName] = score.Details ?? new Dictionary<string, object>();
            }

            return new ThreatIntelScore
            {
                IsKnownMalicious = malicious,
                Score = maxScore,
                MatchingFeeds = feeds,
                LastSeen = lastSeen == DateTimeOffset.MinValue ? null : lastSeen,
                Details = details
            };
        }

        private async Task<ThreatIntelScore?> TryGetFromCacheAsync(string cacheKey)
        {
            try
            {
                var cached = await _redisClient.StringGetAsync(cacheKey);
                if (string.IsNullOrWhiteSpace(cached))
                {
                    return null;
                }

                return JsonSerializer.Deserialize<ThreatIntelScore>(cached, SerializerOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached threat intel entry for key {CacheKey}", cacheKey);
                await _redisClient.KeyDeleteAsync(cacheKey);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving threat intel cache for key {CacheKey}", cacheKey);
                return null;
            }
        }

        private async Task CacheAsync(string cacheKey, ThreatIntelScore score)
        {
            try
            {
                var ttl = DetermineCacheTtl(score);
                var payload = JsonSerializer.Serialize(score, SerializerOptions);
                await _redisClient.StringSetAsync(cacheKey, payload, ttl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache threat intel result for key {CacheKey}", cacheKey);
            }
        }

        private TimeSpan DetermineCacheTtl(ThreatIntelScore score)
        {
            if (score.IsKnownMalicious || score.Score >= _options.MaliciousScoreThreshold)
            {
                return TimeSpan.FromDays(_options.MaliciousCacheTtlDays);
            }

            if (score.Details.TryGetValue("status", out var statusObj))
            {
                var status = statusObj?.ToString();
                if (string.Equals(status, "not_found", StringComparison.OrdinalIgnoreCase))
                {
                    return TimeSpan.FromHours(_options.NotFoundCacheTtlHours);
                }
            }

            return TimeSpan.FromHours(_options.CleanCacheTtlHours);
        }

        private static ThreatIntelScore CreateDisabledScore() => new()
        {
            IsKnownMalicious = false,
            Score = 0,
            MatchingFeeds = Array.Empty<string>(),
            Details = new Dictionary<string, object>
            {
                ["status"] = "disabled"
            }
        };

        private static ThreatIntelScore CreateInvalidScore(string reason) => new()
        {
            IsKnownMalicious = false,
            Score = 0,
            MatchingFeeds = Array.Empty<string>(),
            Details = new Dictionary<string, object>
            {
                ["status"] = reason
            }
        };
    }
}
