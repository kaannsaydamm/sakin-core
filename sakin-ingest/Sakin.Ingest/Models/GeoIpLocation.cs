using System.Text.Json.Serialization;

namespace Sakin.Ingest.Models;

public record GeoIpLocation
{
    [JsonPropertyName("country")]
    public string Country { get; init; } = string.Empty;

    [JsonPropertyName("countryCode")]
    public string CountryCode { get; init; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; init; } = string.Empty;

    [JsonPropertyName("lat")]
    public double? Latitude { get; init; }

    [JsonPropertyName("lon")]
    public double? Longitude { get; init; }

    [JsonPropertyName("timezone")]
    public string Timezone { get; init; } = string.Empty;

    [JsonPropertyName("isPrivate")]
    public bool IsPrivate { get; init; }
}