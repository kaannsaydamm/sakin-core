# Sakin Platform - Bootstrap Script (PowerShell)
# This script downloads MaxMind GeoLite2 database placeholder and prepares the development environment

$ErrorActionPreference = 'Stop'

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = 'White'
    )
    
    $colorMap = @{
        'Blue' = 'Cyan'
        'Green' = 'Green'
        'Yellow' = 'Yellow'
        'Red' = 'Red'
    }
    
    $fgColor = $colorMap[$Color]
    if ($fgColor) {
        Write-Host $Message -ForegroundColor $fgColor
    } else {
        Write-Host $Message
    }
}

Write-ColorOutput "🚀 Sakin Platform Bootstrap" "Blue"
Write-Host "============================"
Write-Host ""

# Determine project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$DataDir = Join-Path $ProjectRoot "data"
$GeoIpDir = Join-Path $DataDir "geoip"

Write-ColorOutput "📂 Creating data directories..." "Blue"
New-Item -ItemType Directory -Force -Path $GeoIpDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataDir "logs") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataDir "exports") | Out-Null

Write-ColorOutput "📥 Setting up MaxMind GeoLite2 database..." "Blue"
Write-Host ""

# Check if GeoLite2 databases already exist
$cityDb = Join-Path $GeoIpDir "GeoLite2-City.mmdb"
$asnDb = Join-Path $GeoIpDir "GeoLite2-ASN.mmdb"

if ((Test-Path $cityDb) -and (Test-Path $asnDb)) {
    Write-ColorOutput "✅ GeoLite2 databases already exist" "Green"
} else {
    Write-ColorOutput "ℹ️  MaxMind GeoLite2 Database Setup" "Yellow"
    Write-Host ""
    Write-Host "MaxMind GeoLite2 databases require a free license key to download."
    Write-Host "Visit: https://dev.maxmind.com/geoip/geolite2-free-geolocation-data"
    Write-Host ""
    Write-Host "For development, we'll create placeholder files."
    Write-Host "To use real GeoIP data:"
    Write-Host "  1. Sign up for a free MaxMind account"
    Write-Host "  2. Download GeoLite2-City.mmdb and GeoLite2-ASN.mmdb"
    Write-Host "  3. Place them in: $GeoIpDir"
    Write-Host ""
    
    Write-ColorOutput "📝 Creating placeholder database files..." "Blue"
    
    # Create README
    $readmeContent = @'
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

```powershell
# Install using Chocolatey
choco install geoipupdate

# Or download from: https://github.com/maxmind/geoipupdate/releases

# Configure with your license key
# Edit C:\ProgramData\MaxMind\GeoIPUpdate\GeoIP.conf

# Run update
geoipupdate -d $GeoIpDir
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
'@
    
    Set-Content -Path (Join-Path $GeoIpDir "README.md") -Value $readmeContent
    
    # Create placeholder files
    $placeholderCity = @'
This is a placeholder file.
Replace with actual GeoLite2-City.mmdb from MaxMind.
See README.md in this directory for instructions.
'@
    
    $placeholderAsn = @'
This is a placeholder file.
Replace with actual GeoLite2-ASN.mmdb from MaxMind.
See README.md in this directory for instructions.
'@
    
    Set-Content -Path (Join-Path $GeoIpDir "GeoLite2-City.mmdb.placeholder") -Value $placeholderCity
    Set-Content -Path (Join-Path $GeoIpDir "GeoLite2-ASN.mmdb.placeholder") -Value $placeholderAsn
    
    Write-ColorOutput "✅ Placeholder files created in: $GeoIpDir" "Green"
}

Write-Host ""
Write-ColorOutput "🔧 Checking development tools..." "Blue"

# Check for required tools
$missingTools = @()

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    $missingTools += "docker"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $missingTools += "dotnet"
}

if ($missingTools.Count -eq 0) {
    Write-ColorOutput "✅ All required tools are installed" "Green"
} else {
    Write-ColorOutput "⚠️  Missing tools: $($missingTools -join ', ')" "Yellow"
    Write-Host "Please install missing tools before continuing."
}

Write-Host ""
Write-ColorOutput "📋 Environment Summary" "Blue"
Write-Host "----------------------"
Write-Host "Project Root: $ProjectRoot"
Write-Host "Data Directory: $DataDir"
Write-Host "GeoIP Directory: $GeoIpDir"
Write-Host ""

if ($missingTools.Count -eq 0) {
    Write-ColorOutput "🎉 Bootstrap complete!" "Green"
    Write-Host ""
    Write-Host "Next steps:"
    Write-Host "  1. Start infrastructure: .\dev-tools.ps1 up"
    Write-Host "  2. Initialize services: .\dev-tools.ps1 init"
    Write-Host "  3. Run tests: .\dev-tools.ps1 test"
    Write-Host "  4. Or use: .\dev-tools.ps1 dev-setup (does all of the above)"
} else {
    Write-ColorOutput "⚠️  Bootstrap incomplete - install missing tools" "Yellow"
    exit 1
}
