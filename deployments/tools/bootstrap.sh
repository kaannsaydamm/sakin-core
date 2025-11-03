#!/bin/bash
# Sakin Platform - Bootstrap Script
# This script downloads MaxMind GeoLite2 database placeholder and prepares the development environment

set -e

BLUE='\033[0;34m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${BLUE}🚀 Sakin Platform Bootstrap${NC}"
echo "============================"
echo ""

# Determine project root (two levels up from tools directory)
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
DATA_DIR="$PROJECT_ROOT/data"
GEOIP_DIR="$DATA_DIR/geoip"

echo -e "${BLUE}📂 Creating data directories...${NC}"
mkdir -p "$GEOIP_DIR"
mkdir -p "$DATA_DIR/logs"
mkdir -p "$DATA_DIR/exports"

echo -e "${BLUE}📥 Setting up MaxMind GeoLite2 database...${NC}"
echo ""

# Check if GeoLite2 databases already exist
if [ -f "$GEOIP_DIR/GeoLite2-City.mmdb" ] && [ -f "$GEOIP_DIR/GeoLite2-ASN.mmdb" ]; then
    echo -e "${GREEN}✅ GeoLite2 databases already exist${NC}"
else
    echo -e "${YELLOW}ℹ️  MaxMind GeoLite2 Database Setup${NC}"
    echo ""
    echo "MaxMind GeoLite2 databases require a free license key to download."
    echo "Visit: https://dev.maxmind.com/geoip/geolite2-free-geolocation-data"
    echo ""
    echo "For development, we'll create placeholder files."
    echo "To use real GeoIP data:"
    echo "  1. Sign up for a free MaxMind account"
    echo "  2. Download GeoLite2-City.mmdb and GeoLite2-ASN.mmdb"
    echo "  3. Place them in: $GEOIP_DIR/"
    echo ""
    
    # Create placeholder files
    echo -e "${BLUE}📝 Creating placeholder database files...${NC}"
    
    cat > "$GEOIP_DIR/README.md" << 'EOF'
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
EOF

    # Create placeholder MMDB files
    cat > "$GEOIP_DIR/GeoLite2-City.mmdb.placeholder" << 'EOF'
This is a placeholder file.
Replace with actual GeoLite2-City.mmdb from MaxMind.
See README.md in this directory for instructions.
EOF

    cat > "$GEOIP_DIR/GeoLite2-ASN.mmdb.placeholder" << 'EOF'
This is a placeholder file.
Replace with actual GeoLite2-ASN.mmdb from MaxMind.
See README.md in this directory for instructions.
EOF

    echo -e "${GREEN}✅ Placeholder files created in: $GEOIP_DIR${NC}"
fi

echo ""
echo -e "${BLUE}🔧 Checking development tools...${NC}"

# Check for required tools
MISSING_TOOLS=()

if ! command -v docker &> /dev/null; then
    MISSING_TOOLS+=("docker")
fi

if ! command -v dotnet &> /dev/null; then
    MISSING_TOOLS+=("dotnet")
fi

if [ ${#MISSING_TOOLS[@]} -eq 0 ]; then
    echo -e "${GREEN}✅ All required tools are installed${NC}"
else
    echo -e "${YELLOW}⚠️  Missing tools: ${MISSING_TOOLS[*]}${NC}"
    echo "Please install missing tools before continuing."
fi

echo ""
echo -e "${BLUE}📋 Environment Summary${NC}"
echo "----------------------"
echo "Project Root: $PROJECT_ROOT"
echo "Data Directory: $DATA_DIR"
echo "GeoIP Directory: $GEOIP_DIR"
echo ""

if [ ${#MISSING_TOOLS[@]} -eq 0 ]; then
    echo -e "${GREEN}🎉 Bootstrap complete!${NC}"
    echo ""
    echo "Next steps:"
    echo "  1. Start infrastructure: make up"
    echo "  2. Initialize services: make init"
    echo "  3. Run tests: make test"
    echo "  4. Or use: make dev-setup (does all of the above)"
else
    echo -e "${YELLOW}⚠️  Bootstrap incomplete - install missing tools${NC}"
    exit 1
fi
