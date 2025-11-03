# MaxMind GeoLite2 Databases

This directory should contain MaxMind GeoLite2 database files for IP geolocation.

## Required Files

- `GeoLite2-City.mmdb` - City-level geolocation database
- `GeoLite2-ASN.mmdb` - ASN (Autonomous System Number) database

## How to Obtain

1. Sign up for a free MaxMind account at:
   https://www.maxmind.com/en/geolite2/signup

2. Generate a license key in your account settings

3. Download the databases:
   - Go to: https://www.maxmind.com/en/accounts/current/geoip/downloads
   - Download "GeoLite2 City" (MMDB format)
   - Download "GeoLite2 ASN" (MMDB format)

4. Extract and place the `.mmdb` files in this directory

## Automated Download (Alternative)

If you have a MaxMind license key, you can use the GeoIP Update tool:

```bash
# Install geoipupdate
# Ubuntu/Debian: apt-get install geoipupdate
# macOS: brew install geoipupdate

# Configure with your license key
# Edit /usr/local/etc/GeoIP.conf or ~/.geoipupdate/GeoIP.conf

# Run update
geoipupdate -d /path/to/this/directory
```

## Usage in Sakin Platform

The GeoIP databases are used by the `sakin-ingest` service to enrich network events with:
- Geographic location (country, city, coordinates)
- ISP and organization information
- ASN details

## Updating Databases

MaxMind updates GeoLite2 databases twice a week (Tuesdays and Fridays).
Consider setting up automated updates using `geoipupdate` in production.

## License

GeoLite2 databases are distributed under the Creative Commons Attribution-ShareAlike 4.0 International License.
See: https://dev.maxmind.com/geoip/geolite2-free-geolocation-data
