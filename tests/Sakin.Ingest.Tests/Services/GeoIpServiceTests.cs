using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Sakin.Ingest.Configuration;
using Sakin.Ingest.Models;
using Sakin.Ingest.Services;
using Xunit;

namespace Sakin.Ingest.Tests.Services;

public class GeoIpServiceTests : IDisposable
{
    private readonly Mock<ILogger<GeoIpService>> _loggerMock;
    private readonly IMemoryCache _cache;
    private readonly string _tempDatabasePath;

    public GeoIpServiceTests()
    {
        _loggerMock = new Mock<ILogger<GeoIpService>>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _tempDatabasePath = Path.Combine(Path.GetTempPath(), $"test_geolite2_{Guid.NewGuid()}.mmdb");
    }

    [Fact]
    public void Constructor_WithMissingDatabase_ShouldLogErrorAndDisableService()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("GeoIp:Enabled", "true"),
                new KeyValuePair<string, string>("GeoIp:DatabasePath", "/nonexistent/path.mmdb")
            })
            .Build();

        // Act
        var service = new GeoIpService(config, _cache, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("GeoIP database")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithDisabledGeoIp_ShouldNotTryToLoadDatabase()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("GeoIp:Enabled", "false")
            })
            .Build();

        // Act
        var service = new GeoIpService(config, _cache, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("GeoIP enrichment is disabled")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Lookup_WithPrivateIp_ShouldReturnPrivateLocation()
    {
        // Arrange
        var config = CreateConfig(enabled: true);
        var service = new GeoIpService(config, _cache, _loggerMock.Object);

        // Act
        var result = service.Lookup("10.0.0.1");

        // Assert
        result.Should().NotBeNull();
        result!.IsPrivate.Should().BeTrue();
        result.Country.Should().Be("Private");
        result.CountryCode.Should().Be("PR");
        result.City.Should().Be("Private Network");
    }

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("10.255.255.254")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.254")]
    [InlineData("192.168.0.1")]
    [InlineData("192.168.255.254")]
    [InlineData("169.254.0.1")]
    [InlineData("127.0.0.1")]
    public void Lookup_WithVariousPrivateIps_ShouldReturnPrivateLocation(string ipAddress)
    {
        // Arrange
        var config = CreateConfig(enabled: true);
        var service = new GeoIpService(config, _cache, _loggerMock.Object);

        // Act
        var result = service.Lookup(ipAddress);

        // Assert
        result.Should().NotBeNull();
        result!.IsPrivate.Should().BeTrue();
        result.Country.Should().Be("Private");
    }

    [Fact]
    public void Lookup_WithInvalidIp_ShouldReturnNull()
    {
        // Arrange
        var config = CreateConfig(enabled: true);
        var service = new GeoIpService(config, _cache, _loggerMock.Object);

        // Act
        var result = service.Lookup("invalid.ip.address");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Lookup_WithNullIp_ShouldReturnNull()
    {
        // Arrange
        var config = CreateConfig(enabled: true);
        var service = new GeoIpService(config, _cache, _loggerMock.Object);

        // Act
        var result = service.Lookup(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Lookup_WithEmptyIp_ShouldReturnNull()
    {
        // Arrange
        var config = CreateConfig(enabled: true);
        var service = new GeoIpService(config, _cache, _loggerMock.Object);

        // Act
        var result = service.Lookup("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Lookup_WithDisabledService_ShouldReturnNull()
    {
        // Arrange
        var config = CreateConfig(enabled: false);
        var service = new GeoIpService(config, _cache, _loggerMock.Object);

        // Act
        var result = service.Lookup("8.8.8.8");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Lookup_WithSameIpMultipleTimes_ShouldUseCache()
    {
        // Arrange
        var config = CreateConfig(enabled: true);
        var service = new GeoIpService(config, _cache, _loggerMock.Object);
        var ipAddress = "10.0.0.1";

        // Act
        var result1 = service.Lookup(ipAddress);
        var result2 = service.Lookup(ipAddress);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Should().Be(result2); // Should be the same object from cache

        // Verify that cache contains the result
        var cacheKey = $"geoip:{ipAddress}";
        _cache.TryGetValue(cacheKey, out GeoIpLocation? cachedResult);
        cachedResult.Should().NotBeNull();
        cachedResult.Should().Be(result1);
    }

    [Fact]
    public void Lookup_WithPublicIp_WhenDatabaseNotAvailable_ShouldReturnNull()
    {
        // Arrange
        var config = CreateConfig(enabled: true, databasePath: "/nonexistent/path.mmdb");
        var service = new GeoIpService(config, _cache, _loggerMock.Object);

        // Act
        var result = service.Lookup("8.8.8.8");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Lookup_WithPrivateIp_ShouldCacheResult()
    {
        // Arrange
        var config = CreateConfig(enabled: true);
        var service = new GeoIpService(config, _cache, _loggerMock.Object);
        var ipAddress = "192.168.1.1";

        // Act
        var result1 = service.Lookup(ipAddress);
        var result2 = service.Lookup(ipAddress);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Should().Be(result2);

        // Verify cache entry
        var cacheKey = $"geoip:{ipAddress}";
        _cache.TryGetValue(cacheKey, out GeoIpLocation? cachedResult);
        cachedResult.Should().NotBeNull();
        cachedResult!.IsPrivate.Should().BeTrue();
    }

    [Fact]
    public void GeoIpLocation_ShouldSerializeToJsonCorrectly()
    {
        // Arrange
        var location = new GeoIpLocation
        {
            Country = "United States",
            CountryCode = "US",
            City = "New York",
            Latitude = 40.7128,
            Longitude = -74.0060,
            Timezone = "America/New_York",
            IsPrivate = false
        };

        // Act
        var json = JsonSerializer.Serialize(location);
        var deserialized = JsonSerializer.Deserialize<GeoIpLocation>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Country.Should().Be("United States");
        deserialized.CountryCode.Should().Be("US");
        deserialized.City.Should().Be("New York");
        deserialized.Latitude.Should().Be(40.7128);
        deserialized.Longitude.Should().Be(-74.0060);
        deserialized.Timezone.Should().Be("America/New_York");
        deserialized.IsPrivate.Should().BeFalse();
    }

    private IConfiguration CreateConfig(bool enabled = true, string databasePath = "/data/GeoLite2-City.mmdb")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("GeoIp:Enabled", enabled.ToString()),
                new KeyValuePair<string, string>("GeoIp:DatabasePath", databasePath),
                new KeyValuePair<string, string>("GeoIp:CacheTtlSeconds", "3600"),
                new KeyValuePair<string, string>("GeoIp:CacheMaxSize", "10000")
            })
            .Build();
    }

    public void Dispose()
    {
        _cache.Dispose();
        
        if (File.Exists(_tempDatabasePath))
        {
            File.Delete(_tempDatabasePath);
        }
    }
}