# Sakin Platform - Development Environment PowerShell Tools
# This script provides convenient commands for local development on Windows

param(
    [Parameter(Position=0)]
    [ValidateSet('help', 'up', 'down', 'restart', 'logs', 'ps', 'test', 'build', 'clean', 
                 'lint', 'format', 'bootstrap', 'verify', 'init', 'stop-clean', 'dev-setup',
                 'check', 'rebuild', 'db-shell', 'redis-cli', 'kafka-topics', 
                 'opensearch-health', 'clickhouse-client', 'all')]
    [string]$Command = 'help',
    
    [Parameter()]
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'

# Colors
$Script:Colors = @{
    Blue = 'Cyan'
    Green = 'Green'
    Yellow = 'Yellow'
    Red = 'Red'
}

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = 'White'
    )
    
    $colorName = $Script:Colors[$Color]
    if ($colorName) {
        Write-Host $Message -ForegroundColor $colorName
    } else {
        Write-Host $Message
    }
}

# Determine project paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$DeploymentsDir = Split-Path -Parent $ScriptDir
$ProjectRoot = Split-Path -Parent $DeploymentsDir
$ComposeFile = Join-Path $DeploymentsDir "docker-compose.dev.yml"
$SolutionFile = Join-Path $ProjectRoot "SAKINCore-CS.sln"

function Show-Help {
    Write-ColorOutput "Sakin Platform - Development Tools" "Blue"
    Write-Host "====================================="
    Write-Host ""
    Write-ColorOutput "Available commands:" "Green"
    Write-Host ""
    Write-ColorOutput "  help              " "Yellow" -NoNewline
    Write-Host "Show this help message"
    Write-ColorOutput "  up                " "Yellow" -NoNewline
    Write-Host "Start all infrastructure services"
    Write-ColorOutput "  down              " "Yellow" -NoNewline
    Write-Host "Stop all services (preserves data)"
    Write-ColorOutput "  stop-clean        " "Yellow" -NoNewline
    Write-Host "Stop all services and remove volumes"
    Write-ColorOutput "  restart           " "Yellow" -NoNewline
    Write-Host "Restart all services"
    Write-ColorOutput "  logs              " "Yellow" -NoNewline
    Write-Host "Show logs from all services"
    Write-ColorOutput "  ps                " "Yellow" -NoNewline
    Write-Host "Show status of all services"
    Write-ColorOutput "  verify            " "Yellow" -NoNewline
    Write-Host "Verify all services are healthy"
    Write-ColorOutput "  init              " "Yellow" -NoNewline
    Write-Host "Initialize infrastructure (run after first up)"
    Write-ColorOutput "  build             " "Yellow" -NoNewline
    Write-Host "Build all .NET projects"
    Write-ColorOutput "  test              " "Yellow" -NoNewline
    Write-Host "Run all tests"
    Write-ColorOutput "  lint              " "Yellow" -NoNewline
    Write-Host "Check code formatting"
    Write-ColorOutput "  format            " "Yellow" -NoNewline
    Write-Host "Format code"
    Write-ColorOutput "  clean             " "Yellow" -NoNewline
    Write-Host "Clean build artifacts"
    Write-ColorOutput "  bootstrap         " "Yellow" -NoNewline
    Write-Host "Download MaxMind GeoLite DB placeholder"
    Write-ColorOutput "  dev-setup         " "Yellow" -NoNewline
    Write-Host "Complete development setup"
    Write-ColorOutput "  check             " "Yellow" -NoNewline
    Write-Host "Run linting and tests"
    Write-ColorOutput "  rebuild           " "Yellow" -NoNewline
    Write-Host "Clean and rebuild all projects"
    Write-ColorOutput "  all               " "Yellow" -NoNewline
    Write-Host "Run bootstrap, build, and test"
    Write-Host ""
    Write-Host "Usage: .\dev-tools.ps1 <command>"
    Write-Host ""
}

function Invoke-Up {
    Write-ColorOutput "🚀 Starting Sakin development environment..." "Blue"
    Push-Location $DeploymentsDir
    try {
        docker compose -f docker-compose.dev.yml up -d
        Write-ColorOutput "⏳ Waiting for services to be healthy..." "Yellow"
        Start-Sleep -Seconds 5
        Invoke-Verify
    } finally {
        Pop-Location
    }
}

function Invoke-Down {
    Write-ColorOutput "🛑 Stopping Sakin development environment..." "Yellow"
    Push-Location $DeploymentsDir
    try {
        docker compose -f docker-compose.dev.yml down
        Write-ColorOutput "✅ Services stopped (data preserved)" "Green"
    } finally {
        Pop-Location
    }
}

function Invoke-StopClean {
    Write-ColorOutput "⚠️  Stopping services and removing all data..." "Red"
    $confirmation = Read-Host "Are you sure you want to remove all data? (yes/no)"
    if ($confirmation -eq 'yes') {
        Push-Location $DeploymentsDir
        try {
            docker compose -f docker-compose.dev.yml down -v
            Write-ColorOutput "✅ Services stopped and data removed" "Green"
        } finally {
            Pop-Location
        }
    } else {
        Write-Host "Operation cancelled."
    }
}

function Invoke-Restart {
    Invoke-Down
    Invoke-Up
}

function Invoke-Logs {
    Push-Location $DeploymentsDir
    try {
        docker compose -f docker-compose.dev.yml logs -f
    } finally {
        Pop-Location
    }
}

function Invoke-Ps {
    Push-Location $DeploymentsDir
    try {
        docker compose -f docker-compose.dev.yml ps
    } finally {
        Pop-Location
    }
}

function Invoke-Verify {
    Write-ColorOutput "🔍 Verifying services..." "Blue"
    $verifyScript = Join-Path $DeploymentsDir "scripts\verify-services.sh"
    if (Test-Path $verifyScript) {
        bash $verifyScript
    } else {
        Write-ColorOutput "⚠️  Verify script not found" "Yellow"
    }
}

function Invoke-Init {
    Write-ColorOutput "📊 Initializing infrastructure..." "Blue"
    $initScript = Join-Path $DeploymentsDir "scripts\opensearch\init-indices.sh"
    if (Test-Path $initScript) {
        $env:OPENSEARCH_HOST = "localhost:9200"
        bash $initScript
    } else {
        Write-ColorOutput "⚠️  OpenSearch init script not found" "Yellow"
    }
    Write-ColorOutput "✅ Infrastructure initialized" "Green"
}

function Invoke-Build {
    Write-ColorOutput "🔨 Building all .NET projects..." "Blue"
    Push-Location $ProjectRoot
    try {
        dotnet build $SolutionFile
        Write-ColorOutput "✅ Build complete" "Green"
    } finally {
        Pop-Location
    }
}

function Invoke-Test {
    Write-ColorOutput "🧪 Running all tests..." "Blue"
    Push-Location $ProjectRoot
    try {
        dotnet test $SolutionFile
        Write-ColorOutput "✅ Tests complete" "Green"
    } finally {
        Pop-Location
    }
}

function Invoke-Lint {
    Write-ColorOutput "🔍 Checking code formatting..." "Blue"
    Push-Location $ProjectRoot
    try {
        $result = dotnet format $SolutionFile --verify-no-changes
        if ($LASTEXITCODE -ne 0) {
            Write-ColorOutput "⚠️  Code formatting issues found. Run 'format' command to fix." "Yellow"
            exit 1
        }
    } finally {
        Pop-Location
    }
}

function Invoke-Format {
    Write-ColorOutput "✨ Formatting code..." "Blue"
    Push-Location $ProjectRoot
    try {
        dotnet format $SolutionFile
        Write-ColorOutput "✅ Code formatted" "Green"
    } finally {
        Pop-Location
    }
}

function Invoke-Clean {
    Write-ColorOutput "🧹 Cleaning build artifacts..." "Blue"
    Push-Location $ProjectRoot
    try {
        dotnet clean $SolutionFile
        Get-ChildItem -Path $ProjectRoot -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force
        Write-ColorOutput "✅ Clean complete" "Green"
    } finally {
        Pop-Location
    }
}

function Invoke-Bootstrap {
    Write-ColorOutput "📦 Running bootstrap script..." "Blue"
    $bootstrapScript = Join-Path $ScriptDir "bootstrap.ps1"
    if (Test-Path $bootstrapScript) {
        & $bootstrapScript
    } else {
        Write-ColorOutput "⚠️  Bootstrap script not found, using bash version" "Yellow"
        $bashBootstrap = Join-Path $ScriptDir "bootstrap.sh"
        if (Test-Path $bashBootstrap) {
            bash $bashBootstrap
        } else {
            Write-ColorOutput "❌ No bootstrap script found" "Red"
            exit 1
        }
    }
    Write-ColorOutput "✅ Bootstrap complete" "Green"
}

function Invoke-DevSetup {
    Invoke-Bootstrap
    Invoke-Up
    Invoke-Init
    Write-Host ""
    Write-ColorOutput "🎉 Development environment is ready!" "Green"
    Write-Host ""
    Write-ColorOutput "Next steps:" "Blue"
    Write-Host "  1. Run network sensor:"
    Write-Host "     cd $ProjectRoot\sakin-core\services\network-sensor"
    Write-Host "     `$env:Database__Host='localhost'"
    Write-Host "     `$env:Database__Password='postgres_dev_password'"
    Write-Host "     dotnet run"
    Write-Host ""
    Write-Host "  2. Access services:"
    Write-Host "     - OpenSearch Dashboards: http://localhost:5601"
    Write-Host "     - PostgreSQL: localhost:5432"
    Write-Host ""
}

function Invoke-Check {
    Invoke-Lint
    Invoke-Test
}

function Invoke-Rebuild {
    Invoke-Clean
    Invoke-Build
}

function Invoke-All {
    Invoke-Bootstrap
    Invoke-Build
    Invoke-Test
}

function Invoke-DbShell {
    docker exec -it sakin-postgres psql -U postgres -d network_db
}

function Invoke-RedisCliCommand {
    docker exec -it sakin-redis redis-cli -a redis_dev_password
}

function Invoke-KafkaTopics {
    docker exec -it sakin-kafka kafka-topics --bootstrap-server localhost:9092 --list
}

function Invoke-OpenSearchHealth {
    Invoke-RestMethod -Uri "http://localhost:9200/_cluster/health?pretty"
}

function Invoke-ClickHouseClient {
    docker exec -it sakin-clickhouse clickhouse-client --user clickhouse --password clickhouse_dev_password
}

# Main command dispatcher
switch ($Command) {
    'help' { Show-Help }
    'up' { Invoke-Up }
    'down' { Invoke-Down }
    'stop-clean' { Invoke-StopClean }
    'restart' { Invoke-Restart }
    'logs' { Invoke-Logs }
    'ps' { Invoke-Ps }
    'verify' { Invoke-Verify }
    'init' { Invoke-Init }
    'build' { Invoke-Build }
    'test' { Invoke-Test }
    'lint' { Invoke-Lint }
    'format' { Invoke-Format }
    'clean' { Invoke-Clean }
    'bootstrap' { Invoke-Bootstrap }
    'dev-setup' { Invoke-DevSetup }
    'check' { Invoke-Check }
    'rebuild' { Invoke-Rebuild }
    'all' { Invoke-All }
    'db-shell' { Invoke-DbShell }
    'redis-cli' { Invoke-RedisCliCommand }
    'kafka-topics' { Invoke-KafkaTopics }
    'opensearch-health' { Invoke-OpenSearchHealth }
    'clickhouse-client' { Invoke-ClickHouseClient }
    default { Show-Help }
}
