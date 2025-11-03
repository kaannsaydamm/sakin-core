# Sakin Platform - Development Tools

This directory contains automation tools for local development environment setup and management.

## Quick Start

### Linux/macOS (Using Make)

```bash
# Bootstrap the environment (first time setup)
make bootstrap

# Start all infrastructure services
make up

# Initialize services (OpenSearch indices, etc.)
make init

# Or do everything in one command
make dev-setup

# Run tests
make test

# Stop services
make down
```

### Windows (Using PowerShell)

```powershell
# Bootstrap the environment (first time setup)
.\dev-tools.ps1 bootstrap

# Start all infrastructure services
.\dev-tools.ps1 up

# Initialize services (OpenSearch indices, etc.)
.\dev-tools.ps1 init

# Or do everything in one command
.\dev-tools.ps1 dev-setup

# Run tests
.\dev-tools.ps1 test

# Stop services
.\dev-tools.ps1 down
```

## Available Commands

### Infrastructure Management

| Command | Description |
|---------|-------------|
| `up` | Start all Docker infrastructure services |
| `down` | Stop all services (preserves data) |
| `stop-clean` | Stop services and remove all data volumes |
| `restart` | Restart all services |
| `logs` | Show logs from all services (follows) |
| `ps` | Show status of all services |
| `verify` | Verify all services are healthy |
| `init` | Initialize infrastructure (OpenSearch indices, etc.) |

### Development Commands

| Command | Description |
|---------|-------------|
| `build` | Build all .NET projects |
| `test` | Run all unit tests |
| `lint` | Check code formatting |
| `format` | Format code using dotnet format |
| `clean` | Clean build artifacts |
| `bootstrap` | Download MaxMind GeoLite DB placeholder and setup directories |

### Compound Commands

| Command | Description |
|---------|-------------|
| `dev-setup` | Complete setup: bootstrap + up + init |
| `check` | Run linting and tests |
| `rebuild` | Clean and rebuild all projects |
| `all` | Bootstrap + build + test |

### Service Management

| Command | Description |
|---------|-------------|
| `db-shell` | Open PostgreSQL shell |
| `redis-cli` | Open Redis CLI |
| `kafka-topics` | List Kafka topics |
| `opensearch-health` | Check OpenSearch cluster health |
| `clickhouse-client` | Open ClickHouse client |

## Tools Included

### 1. Makefile

The Makefile provides convenient commands for Linux/macOS users. All commands wrap Docker Compose and .NET CLI commands.

**Location:** `deployments/tools/Makefile`

**Usage:**
```bash
cd deployments/tools
make <command>
```

**Examples:**
```bash
# Show help
make help

# Start environment
make up

# Run tests
make test

# Clean everything
make stop-clean
```

### 2. PowerShell Script (dev-tools.ps1)

The PowerShell script provides the same functionality for Windows users.

**Location:** `deployments/tools/dev-tools.ps1`

**Usage:**
```powershell
cd deployments/tools
.\dev-tools.ps1 <command>
```

**Examples:**
```powershell
# Show help
.\dev-tools.ps1 help

# Start environment
.\dev-tools.ps1 up

# Run tests
.\dev-tools.ps1 test

# Clean everything
.\dev-tools.ps1 stop-clean
```

### 3. Bootstrap Scripts

Bootstrap scripts prepare the development environment by:
- Creating necessary data directories
- Setting up MaxMind GeoLite2 database placeholders
- Checking for required development tools
- Providing guidance for obtaining real GeoIP databases

**Linux/macOS:** `bootstrap.sh`
**Windows:** `bootstrap.ps1`

**Usage:**
```bash
# Linux/macOS
./bootstrap.sh

# Windows PowerShell
.\bootstrap.ps1
```

## MaxMind GeoLite2 Setup

The bootstrap scripts create placeholder files for MaxMind GeoLite2 databases. These databases are used for IP geolocation enrichment.

### Getting Real GeoLite2 Databases

1. **Sign up for a free MaxMind account:**
   - Visit: https://www.maxmind.com/en/geolite2/signup

2. **Generate a license key:**
   - Go to your account settings
   - Create a new license key

3. **Download databases:**
   - Visit: https://www.maxmind.com/en/accounts/current/geoip/downloads
   - Download "GeoLite2 City" (MMDB format)
   - Download "GeoLite2 ASN" (MMDB format)

4. **Place files:**
   - Extract and place the `.mmdb` files in: `<project-root>/data/geoip/`
   - Required files:
     - `GeoLite2-City.mmdb`
     - `GeoLite2-ASN.mmdb`

### Automated Updates (Optional)

For production environments, consider using `geoipupdate`:

```bash
# Ubuntu/Debian
apt-get install geoipupdate

# macOS
brew install geoipupdate

# Windows
choco install geoipupdate
```

Configure with your license key and set up automated updates.

## Directory Structure

After running bootstrap, the following structure is created:

```
project-root/
├── data/
│   ├── geoip/
│   │   ├── README.md
│   │   ├── GeoLite2-City.mmdb.placeholder
│   │   └── GeoLite2-ASN.mmdb.placeholder
│   ├── logs/
│   └── exports/
└── deployments/
    └── tools/
        ├── Makefile
        ├── dev-tools.ps1
        ├── bootstrap.sh
        ├── bootstrap.ps1
        └── README.md
```

## Requirements

### Required Tools

- **Docker** & **Docker Compose** - For running infrastructure services
- **.NET 8 SDK** - For building and testing C# projects

### Optional Tools

- **Make** (Linux/macOS) - For using the Makefile
- **PowerShell** (Windows) - For using dev-tools.ps1
- **Bash** (Windows) - Git Bash or WSL for running bash scripts

## Infrastructure Services

The development environment includes:

| Service | Port | Purpose |
|---------|------|---------|
| PostgreSQL | 5432 | Primary database |
| Redis | 6379 | Caching and session storage |
| Zookeeper | 2181 | Kafka coordination |
| Kafka | 9092, 29092 | Event streaming |
| OpenSearch | 9200, 9600 | Search and analytics |
| OpenSearch Dashboards | 5601 | Visualization UI |
| ClickHouse | 8123, 9000 | OLAP analytics |

## Workflow Examples

### First-Time Setup

```bash
# Clone repository
git clone https://github.com/yourusername/sakin-core.git
cd sakin-core/deployments/tools

# Bootstrap and setup everything
make dev-setup

# Or step by step:
make bootstrap  # Setup directories and placeholders
make up         # Start infrastructure
make init       # Initialize services
make test       # Verify everything works
```

### Daily Development

```bash
# Start services
make up

# Make code changes...

# Run tests
make test

# Check formatting
make lint

# Stop services when done
make down
```

### Clean Slate

```bash
# Stop and remove all data
make stop-clean

# Clean build artifacts
make clean

# Start fresh
make dev-setup
```

## Troubleshooting

### Services won't start

```bash
# Check Docker is running
docker info

# View service logs
make logs

# Check service status
make ps

# Verify health
make verify
```

### Tests failing

```bash
# Rebuild everything
make rebuild

# Check if services are healthy
make verify

# View specific service logs
cd ../
docker compose -f docker-compose.dev.yml logs <service-name>
```

### Port conflicts

If you have port conflicts, you can modify the ports in `deployments/docker-compose.dev.yml`.

### Permission issues (Linux)

Network sensor requires elevated privileges:

```bash
cd ../../sakin-core/services/network-sensor
sudo dotnet run
```

## Integration with CI/CD

These tools are designed for local development. CI/CD pipelines use GitHub Actions workflows defined in `.github/workflows/`.

## Contributing

When adding new automation:

1. Update both Makefile and PowerShell script
2. Keep commands consistent between platforms
3. Update this README with new commands
4. Test on both Linux/macOS and Windows

## Support

For issues or questions:
- Check the main [README](../../README.md)
- Review [Docker Setup Guide](../DOCKER_SETUP.md)
- Open an issue on GitHub

## License

See [LICENSE](../../LICENSE) for details.
