using Sakin.Ingest.Models;

namespace Sakin.Ingest.Services;

public interface IGeoIpService
{
    GeoIpLocation? Lookup(string ipAddress);
}