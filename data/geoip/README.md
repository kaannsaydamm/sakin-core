# GeoIP Data Directory

This directory contains the MaxMind GeoLite2 database files for GeoIP enrichment.

## Download GeoLite2-City.mmdb

To enable GeoIP enrichment, download the GeoLite2-City database from:
https://dev.maxmind.com/geoip/geolite2-open-data-locations/

1. Create a free MaxMind account
2. Download the GeoLite2-City.mmdb file
3. Place it in this directory as `GeoLite2-City.mmdb`

The database should be automatically mounted by Docker at `/data/GeoLite2-City.mmdb` inside the ingest container.

## File Structure

```
/data/geoip/
├── README.md                    # This file
└── GeoLite2-City.mmdb          # MaxMind GeoLite2 City database (download separately)
```

## Database Updates

The GeoLite2 database is updated weekly. For production use, consider setting up automated updates to ensure accurate GeoIP data.