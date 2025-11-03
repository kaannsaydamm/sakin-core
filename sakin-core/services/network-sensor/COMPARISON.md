# Project Comparison: SAKINCore-CS vs Sakin.Core.Sensor

## Side-by-Side Comparison

| Aspect | SAKINCore-CS (Legacy) | Sakin.Core.Sensor (New) |
|--------|----------------------|-------------------------|
| **Location** | `/SAKINCore-CS/` | `/sakin-core/services/network-sensor/` |
| **Namespace** | `SAKINCore.*` | `Sakin.Core.Sensor.*` |
| **Architecture** | Static classes | DI with interfaces |
| **Configuration** | Hardcoded strings | appsettings.json + IOptions |
| **Entry Point** | `static void Main()` | `async Task Main()` with Host Builder |
| **Lifecycle** | Manual management | BackgroundService |
| **Testability** | Difficult (static dependencies) | Easy (interface injection) |
| **.NET Target** | .NET 8 | .NET 8 |

## File Mapping

### Old Structure
```
SAKINCore-CS/
├── Program.cs
├── SAKINCore-CS.csproj
├── GlobalSuppressions.cs
├── Handlers/
│   └── Database.cs (static class)
└── Utils/
    ├── PackageInspector.cs (static class)
    └── TLSParser.cs (static class)
```

### New Structure
```
sakin-core/services/network-sensor/
├── Program.cs (with DI setup)
├── Sakin.Core.Sensor.csproj
├── appsettings.json
├── README.md
├── MIGRATION.md
├── Configuration/
│   └── DatabaseOptions.cs
├── Handlers/
│   ├── IDatabaseHandler.cs
│   └── DatabaseHandler.cs (injectable)
├── Services/
│   └── NetworkSensorService.cs
└── Utils/
    ├── IPackageInspector.cs
    ├── PackageInspector.cs (injectable)
    └── TLSParser.cs (static utility)
```

## Code Changes

### DatabaseHandler

**Old (Static):**
```csharp
namespace SAKINCore.Handlers
{
    public static class DatabaseHandler
    {
        public static NpgsqlConnection? InitDB()
        {
            string connString = "Host=localhost;Username=postgres;Password=kaan1980;Database=network_db;Port=5432";
            // ...
        }
    }
}
```

**New (Injectable):**
```csharp
namespace Sakin.Core.Sensor.Handlers
{
    public class DatabaseHandler : IDatabaseHandler
    {
        private readonly DatabaseOptions _options;

        public DatabaseHandler(IOptions<DatabaseOptions> options)
        {
            _options = options.Value;
        }

        public NpgsqlConnection? InitDB()
        {
            string connString = _options.GetConnectionString();
            // ...
        }
    }
}
```

### PackageInspector

**Old (Static):**
```csharp
namespace SAKINCore.Utils
{
    public static class PackageInspector
    {
        public static void MonitorTraffic(...)
        {
            // Direct static call
            DatabaseHandler.SaveSNIAsync(...);
        }
    }
}
```

**New (Injectable):**
```csharp
namespace Sakin.Core.Sensor.Utils
{
    public class PackageInspector : IPackageInspector
    {
        private readonly IDatabaseHandler _databaseHandler;

        public PackageInspector(IDatabaseHandler databaseHandler)
        {
            _databaseHandler = databaseHandler;
        }

        public void MonitorTraffic(...)
        {
            // Injected dependency
            _databaseHandler.SaveSNIAsync(...);
        }
    }
}
```

### Program.cs

**Old (Console):**
```csharp
namespace SAKINCore
{
    class Program
    {
        static void Main(string[] args)
        {
            var interfaces = CaptureDeviceList.Instance;
            var dbConnection = DatabaseHandler.InitDB();
            var wg = new ManualResetEvent(false);
            PackageInspector.MonitorTraffic(interfaces, dbConnection, wg);
            wg.WaitOne();
        }
    }
}
```

**New (Host Builder):**
```csharp
namespace Sakin.Core.Sensor
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            await host.RunAsync();
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory())
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<DatabaseOptions>(
                        context.Configuration.GetSection(DatabaseOptions.SectionName));

                    services.AddSingleton<IDatabaseHandler, DatabaseHandler>();
                    services.AddSingleton<IPackageInspector, PackageInspector>();

                    services.AddHostedService<NetworkSensorService>();
                });
    }
}
```

## Preserved Functionality

All core functionality from the legacy project has been preserved:

1. ✅ Network interface enumeration
2. ✅ Loopback interface skipping
3. ✅ Promiscuous mode packet capture
4. ✅ IPv4 packet processing
5. ✅ HTTP URL extraction via regex
6. ✅ TLS SNI parsing (commented out, ready for development)
7. ✅ PostgreSQL persistence (PacketData and SniData tables)
8. ✅ Debug payload inspection code (commented out)
9. ✅ String cleaning for SNI data

## New Additions

1. ✅ Dependency injection throughout
2. ✅ Configuration management (appsettings.json)
3. ✅ Interfaces for all services (IDatabaseHandler, IPackageInspector)
4. ✅ Background service pattern (NetworkSensorService)
5. ✅ Strongly-typed configuration (DatabaseOptions)
6. ✅ Graceful shutdown handling
7. ✅ Environment variable overrides
8. ✅ Documentation (README, MIGRATION, COMPARISON)

## Testing the Migration

To verify the migration maintains functionality:

```bash
# Build both projects
dotnet build SAKINCore-CS.sln

# Old project
cd SAKINCore-CS
dotnet run

# New project
cd ../sakin-core/services/network-sensor
dotnet run
```

Both should exhibit the same behavior when capturing packets (requires libpcap/WinPcap and database setup).
