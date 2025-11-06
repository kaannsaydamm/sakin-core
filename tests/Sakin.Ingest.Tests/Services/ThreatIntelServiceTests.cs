using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Sakin.Common.Cache;
using Sakin.Common.Configuration;
using Sakin.Common.Models;
using Sakin.ThreatIntelService.Providers;
using Sakin.ThreatIntelService.Services;
using Xunit;

namespace Sakin.Ingest.Tests.Services
{
    public class ThreatIntelServiceTests
    {
        [Fact]
        public async Task ProcessAsync_WithCachedResult_ReturnsCachedScore()
        {
            var mockRedis = new Mock<IRedisClient>();
            var mockRateLimiter = new Mock<IThreatIntelRateLimiter>();
            var mockLogger = new Mock<ILogger<ThreatIntelAggregationService>>();

            var threatIntelScore = new ThreatIntelScore
            {
                IsKnownMalicious = true,
                Score = 95,
                MatchingFeeds = new[] { "OTX" },
                Details = new Dictionary<string, object> { ["status"] = "malicious" }
            };

            var serialized = JsonSerializer.Serialize(threatIntelScore);
            mockRedis.Setup(r => r.StringGetAsync(It.IsAny<string>()))
                .ReturnsAsync(serialized);

            var options = Options.Create(new ThreatIntelOptions());
            var service = new ThreatIntelAggregationService(
                Enumerable.Empty<IThreatIntelProvider>(),
                mockRedis.Object,
                mockRateLimiter.Object,
                options,
                mockLogger.Object);

            var request = new ThreatIntelLookupRequest
            {
                Type = ThreatIntelIndicatorType.Ipv4,
                Value = "1.2.3.4"
            };

            var result = await service.ProcessAsync(request);

            Assert.True(result.IsKnownMalicious);
            Assert.Equal(95, result.Score);
            mockRedis.Verify(r => r.StringGetAsync(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ProcessAsync_WithNoProviders_ReturnsNotFoundScore()
        {
            var mockRedis = new Mock<IRedisClient>();
            mockRedis.Setup(r => r.StringGetAsync(It.IsAny<string>()))
                .ReturnsAsync((string?)null);

            var mockRateLimiter = new Mock<IThreatIntelRateLimiter>();
            var mockLogger = new Mock<ILogger<ThreatIntelAggregationService>>();

            var options = Options.Create(new ThreatIntelOptions());
            var service = new ThreatIntelAggregationService(
                Enumerable.Empty<IThreatIntelProvider>(),
                mockRedis.Object,
                mockRateLimiter.Object,
                options,
                mockLogger.Object);

            var request = new ThreatIntelLookupRequest
            {
                Type = ThreatIntelIndicatorType.Ipv4,
                Value = "1.2.3.4"
            };

            var result = await service.ProcessAsync(request);

            Assert.False(result.IsKnownMalicious);
            Assert.Equal(0, result.Score);
            Assert.Contains("not_found", result.Details.Values.Select(v => v?.ToString()));
        }

        [Fact]
        public async Task ProcessAsync_WithMaliciousProvider_ScoresAboveThreshold()
        {
            var mockRedis = new Mock<IRedisClient>();
            mockRedis.Setup(r => r.StringGetAsync(It.IsAny<string>()))
                .ReturnsAsync((string?)null);
            mockRedis.Setup(r => r.StringSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .ReturnsAsync(true);

            var mockProvider = new Mock<IThreatIntelProvider>();
            mockProvider.Setup(p => p.Name).Returns("MockProvider");
            mockProvider.Setup(p => p.Supports(It.IsAny<ThreatIntelIndicatorType>())).Returns(true);
            mockProvider.Setup(p => p.LookupAsync(It.IsAny<ThreatIntelLookupRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ThreatIntelScore
                {
                    IsKnownMalicious = false,
                    Score = 85,
                    MatchingFeeds = Array.Empty<string>(),
                    Details = new Dictionary<string, object>()
                });

            var mockRateLimiter = new Mock<IThreatIntelRateLimiter>();
            mockRateLimiter.Setup(r => r.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var mockLogger = new Mock<ILogger<ThreatIntelAggregationService>>();

            var options = Options.Create(new ThreatIntelOptions { MaliciousScoreThreshold = 80 });
            var service = new ThreatIntelAggregationService(
                new[] { mockProvider.Object },
                mockRedis.Object,
                mockRateLimiter.Object,
                options,
                mockLogger.Object);

            var request = new ThreatIntelLookupRequest
            {
                Type = ThreatIntelIndicatorType.Ipv4,
                Value = "1.2.3.4"
            };

            var result = await service.ProcessAsync(request);

            Assert.True(result.IsKnownMalicious);
            Assert.Equal(85, result.Score);
        }

        [Fact]
        public async Task ProcessAsync_WithMultipleProviders_AggregatesHighestScore()
        {
            var mockRedis = new Mock<IRedisClient>();
            mockRedis.Setup(r => r.StringGetAsync(It.IsAny<string>()))
                .ReturnsAsync((string?)null);
            mockRedis.Setup(r => r.StringSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .ReturnsAsync(true);

            var mockProvider1 = new Mock<IThreatIntelProvider>();
            mockProvider1.Setup(p => p.Name).Returns("Provider1");
            mockProvider1.Setup(p => p.Supports(It.IsAny<ThreatIntelIndicatorType>())).Returns(true);
            mockProvider1.Setup(p => p.LookupAsync(It.IsAny<ThreatIntelLookupRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ThreatIntelScore
                {
                    IsKnownMalicious = false,
                    Score = 50,
                    MatchingFeeds = Array.Empty<string>(),
                    Details = new Dictionary<string, object>()
                });

            var mockProvider2 = new Mock<IThreatIntelProvider>();
            mockProvider2.Setup(p => p.Name).Returns("Provider2");
            mockProvider2.Setup(p => p.Supports(It.IsAny<ThreatIntelIndicatorType>())).Returns(true);
            mockProvider2.Setup(p => p.LookupAsync(It.IsAny<ThreatIntelLookupRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ThreatIntelScore
                {
                    IsKnownMalicious = false,
                    Score = 90,
                    MatchingFeeds = Array.Empty<string>(),
                    Details = new Dictionary<string, object>()
                });

            var mockRateLimiter = new Mock<IThreatIntelRateLimiter>();
            mockRateLimiter.Setup(r => r.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var mockLogger = new Mock<ILogger<ThreatIntelAggregationService>>();

            var options = Options.Create(new ThreatIntelOptions { MaliciousScoreThreshold = 80 });
            var service = new ThreatIntelAggregationService(
                new[] { mockProvider1.Object, mockProvider2.Object },
                mockRedis.Object,
                mockRateLimiter.Object,
                options,
                mockLogger.Object);

            var request = new ThreatIntelLookupRequest
            {
                Type = ThreatIntelIndicatorType.Ipv4,
                Value = "1.2.3.4"
            };

            var result = await service.ProcessAsync(request);

            Assert.True(result.IsKnownMalicious);
            Assert.Equal(90, result.Score);
            Assert.Contains("Provider1", result.MatchingFeeds);
            Assert.Contains("Provider2", result.MatchingFeeds);
        }

        [Fact]
        public async Task ProcessAsync_WithRateLimitExceeded_SkipsProvider()
        {
            var mockRedis = new Mock<IRedisClient>();
            mockRedis.Setup(r => r.StringGetAsync(It.IsAny<string>()))
                .ReturnsAsync((string?)null);
            mockRedis.Setup(r => r.StringSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .ReturnsAsync(true);

            var mockProvider = new Mock<IThreatIntelProvider>();
            mockProvider.Setup(p => p.Name).Returns("RateLimitedProvider");
            mockProvider.Setup(p => p.Supports(It.IsAny<ThreatIntelIndicatorType>())).Returns(true);
            mockProvider.Setup(p => p.LookupAsync(It.IsAny<ThreatIntelLookupRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ThreatIntelScore
                {
                    IsKnownMalicious = true,
                    Score = 95,
                    MatchingFeeds = Array.Empty<string>(),
                    Details = new Dictionary<string, object>()
                });

            var mockRateLimiter = new Mock<IThreatIntelRateLimiter>();
            mockRateLimiter.Setup(r => r.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var mockLogger = new Mock<ILogger<ThreatIntelAggregationService>>();

            var options = Options.Create(new ThreatIntelOptions { MaliciousScoreThreshold = 80 });
            var service = new ThreatIntelAggregationService(
                new[] { mockProvider.Object },
                mockRedis.Object,
                mockRateLimiter.Object,
                options,
                mockLogger.Object);

            var request = new ThreatIntelLookupRequest
            {
                Type = ThreatIntelIndicatorType.Ipv4,
                Value = "1.2.3.4"
            };

            var result = await service.ProcessAsync(request);

            Assert.False(result.IsKnownMalicious);
            Assert.Equal(0, result.Score);
            mockProvider.Verify(p => p.LookupAsync(It.IsAny<ThreatIntelLookupRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessAsync_WithProviderException_ContinuesToNextProvider()
        {
            var mockRedis = new Mock<IRedisClient>();
            mockRedis.Setup(r => r.StringGetAsync(It.IsAny<string>()))
                .ReturnsAsync((string?)null);
            mockRedis.Setup(r => r.StringSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .ReturnsAsync(true);

            var mockFailingProvider = new Mock<IThreatIntelProvider>();
            mockFailingProvider.Setup(p => p.Name).Returns("FailingProvider");
            mockFailingProvider.Setup(p => p.Supports(It.IsAny<ThreatIntelIndicatorType>())).Returns(true);
            mockFailingProvider.Setup(p => p.LookupAsync(It.IsAny<ThreatIntelLookupRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Connection failed"));

            var mockWorkingProvider = new Mock<IThreatIntelProvider>();
            mockWorkingProvider.Setup(p => p.Name).Returns("WorkingProvider");
            mockWorkingProvider.Setup(p => p.Supports(It.IsAny<ThreatIntelIndicatorType>())).Returns(true);
            mockWorkingProvider.Setup(p => p.LookupAsync(It.IsAny<ThreatIntelLookupRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ThreatIntelScore
                {
                    IsKnownMalicious = true,
                    Score = 85,
                    MatchingFeeds = Array.Empty<string>(),
                    Details = new Dictionary<string, object>()
                });

            var mockRateLimiter = new Mock<IThreatIntelRateLimiter>();
            mockRateLimiter.Setup(r => r.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var mockLogger = new Mock<ILogger<ThreatIntelAggregationService>>();

            var options = Options.Create(new ThreatIntelOptions { MaliciousScoreThreshold = 80 });
            var service = new ThreatIntelAggregationService(
                new[] { mockFailingProvider.Object, mockWorkingProvider.Object },
                mockRedis.Object,
                mockRateLimiter.Object,
                options,
                mockLogger.Object);

            var request = new ThreatIntelLookupRequest
            {
                Type = ThreatIntelIndicatorType.Ipv4,
                Value = "1.2.3.4"
            };

            var result = await service.ProcessAsync(request);

            Assert.True(result.IsKnownMalicious);
            Assert.Equal(85, result.Score);
            mockWorkingProvider.Verify(p => p.LookupAsync(It.IsAny<ThreatIntelLookupRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ProcessAsync_WithCleanScore_CachesDurationIsOneHour()
        {
            var mockRedis = new Mock<IRedisClient>();
            mockRedis.Setup(r => r.StringGetAsync(It.IsAny<string>()))
                .ReturnsAsync((string?)null);
            mockRedis.Setup(r => r.StringSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .ReturnsAsync(true);

            TimeSpan? capturedTtl = null;
            mockRedis.Setup(r => r.StringSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Callback<string, string, TimeSpan?>((key, value, ttl) => capturedTtl = ttl)
                .ReturnsAsync(true);

            var mockProvider = new Mock<IThreatIntelProvider>();
            mockProvider.Setup(p => p.Name).Returns("Provider");
            mockProvider.Setup(p => p.Supports(It.IsAny<ThreatIntelIndicatorType>())).Returns(true);
            mockProvider.Setup(p => p.LookupAsync(It.IsAny<ThreatIntelLookupRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ThreatIntelScore
                {
                    IsKnownMalicious = false,
                    Score = 20,
                    MatchingFeeds = Array.Empty<string>(),
                    Details = new Dictionary<string, object>()
                });

            var mockRateLimiter = new Mock<IThreatIntelRateLimiter>();
            mockRateLimiter.Setup(r => r.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var mockLogger = new Mock<ILogger<ThreatIntelAggregationService>>();

            var options = Options.Create(new ThreatIntelOptions { CleanCacheTtlHours = 1 });
            var service = new ThreatIntelAggregationService(
                new[] { mockProvider.Object },
                mockRedis.Object,
                mockRateLimiter.Object,
                options,
                mockLogger.Object);

            var request = new ThreatIntelLookupRequest
            {
                Type = ThreatIntelIndicatorType.Ipv4,
                Value = "1.2.3.4"
            };

            await service.ProcessAsync(request);

            Assert.NotNull(capturedTtl);
            Assert.Equal(TimeSpan.FromHours(1), capturedTtl);
        }

        [Fact]
        public async Task ProcessAsync_WithMaliciousScore_CachesDurationIsSevenDays()
        {
            var mockRedis = new Mock<IRedisClient>();
            mockRedis.Setup(r => r.StringGetAsync(It.IsAny<string>()))
                .ReturnsAsync((string?)null);

            TimeSpan? capturedTtl = null;
            mockRedis.Setup(r => r.StringSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Callback<string, string, TimeSpan?>((key, value, ttl) => capturedTtl = ttl)
                .ReturnsAsync(true);

            var mockProvider = new Mock<IThreatIntelProvider>();
            mockProvider.Setup(p => p.Name).Returns("Provider");
            mockProvider.Setup(p => p.Supports(It.IsAny<ThreatIntelIndicatorType>())).Returns(true);
            mockProvider.Setup(p => p.LookupAsync(It.IsAny<ThreatIntelLookupRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ThreatIntelScore
                {
                    IsKnownMalicious = false,
                    Score = 90,
                    MatchingFeeds = Array.Empty<string>(),
                    Details = new Dictionary<string, object>()
                });

            var mockRateLimiter = new Mock<IThreatIntelRateLimiter>();
            mockRateLimiter.Setup(r => r.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var mockLogger = new Mock<ILogger<ThreatIntelAggregationService>>();

            var options = Options.Create(new ThreatIntelOptions { MaliciousScoreThreshold = 80, MaliciousCacheTtlDays = 7 });
            var service = new ThreatIntelAggregationService(
                new[] { mockProvider.Object },
                mockRedis.Object,
                mockRateLimiter.Object,
                options,
                mockLogger.Object);

            var request = new ThreatIntelLookupRequest
            {
                Type = ThreatIntelIndicatorType.Ipv4,
                Value = "1.2.3.4"
            };

            await service.ProcessAsync(request);

            Assert.NotNull(capturedTtl);
            Assert.Equal(TimeSpan.FromDays(7), capturedTtl);
        }
    }
}
