using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;
using Sakin.Ingest.Configuration;
using Sakin.Ingest.Models;

namespace Sakin.Ingest.Services;

public class GeoIpService : IGeoIpService
{
    private readonly DatabaseReader? _databaseReader;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GeoIpService> _logger;
    private readonly GeoIpOptions _options;

    public GeoIpService(
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<GeoIpService> logger)
    {
        _cache = cache;
        _logger = logger;
        _options = new GeoIpOptions();
        configuration.GetSection(GeoIpOptions.SectionName).Bind(_options);

        if (_options.Enabled)
        {
            try
            {
                _databaseReader = new DatabaseReader(_options.DatabasePath);
                _logger.LogInformation("GeoIP database loaded successfully from {DatabasePath}", _options.DatabasePath);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "GeoIP database file not found at {DatabasePath}. GeoIP enrichment will be disabled.", _options.DatabasePath);
                _databaseReader = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load GeoIP database from {DatabasePath}. GeoIP enrichment will be disabled.", _options.DatabasePath);
                _databaseReader = null;
            }
        }
        else
        {
            _logger.LogInformation("GeoIP enrichment is disabled in configuration");
            _databaseReader = null;
        }
    }

    public GeoIpLocation? Lookup(string ipAddress)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        // Check cache first
        var cacheKey = $"geoip:{ipAddress}";
        if (_cache.TryGetValue(cacheKey, out GeoIpLocation? cachedResult))
        {
            return cachedResult;
        }

        try
        {
            if (!IPAddress.TryParse(ipAddress, out var ip))
            {
                _logger.LogWarning("Invalid IP address format: {IpAddress}", ipAddress);
                return null;
            }

            // Check if it's a private IP (can be done without database)
            if (IsPrivateIp(ip))
            {
                var privateLocation = new GeoIpLocation
                {
                    Country = "Private",
                    CountryCode = "PR",
                    City = "Private Network",
                    IsPrivate = true
                };

                CacheResult(cacheKey, privateLocation);
                return privateLocation;
            }

            // For public IPs, we need the database
            if (_databaseReader == null)
            {
                _logger.LogDebug("GeoIP database not available for public IP lookup: {IpAddress}", ipAddress);
                return null;
            }

            // Try to lookup in the database
            if (_databaseReader.TryCity(ip, out var response) && response != null)
            {
                var location = new GeoIpLocation
                {
                    Country = response.Country?.Name ?? "Unknown",
                    CountryCode = response.Country?.IsoCode ?? "XX",
                    City = response.City?.Name ?? "Unknown",
                    Latitude = response.Location?.Latitude,
                    Longitude = response.Location?.Longitude,
                    Timezone = response.Location?.TimeZone ?? "UTC",
                    IsPrivate = false
                };

                CacheResult(cacheKey, location);
                return location;
            }
            else
            {
                _logger.LogDebug("No GeoIP data found for IP address: {IpAddress}", ipAddress);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during GeoIP lookup for IP address: {IpAddress}", ipAddress);
            return null;
        }
    }

    private static bool IsPrivateIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        var bytes = ip.GetAddressBytes();
        
        // 10.0.0.0/8
        if (bytes[0] == 10)
        {
            return true;
        }

        // 172.16.0.0/12
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        {
            return true;
        }

        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168)
        {
            return true;
        }

        // 169.254.0.0/16 (APIPA)
        if (bytes[0] == 169 && bytes[1] == 254)
        {
            return true;
        }

        return false;
    }

    private void CacheResult(string cacheKey, GeoIpLocation location)
    {
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(_options.CacheTtlSeconds))
            .SetSize(1);

        _cache.Set(cacheKey, location, cacheEntryOptions);
    }

    public void Dispose()
    {
        _databaseReader?.Dispose();
    }
}