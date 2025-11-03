# Migration Guide: SAKINCore-CS to Sakin.Core.Sensor

This document describes the migration from the legacy SAKINCore-CS project to the new Sakin.Core.Sensor service with modern .NET 8 patterns.

## Key Changes

### 1. Project Location & Structure
- **Old**: `/SAKINCore-CS/`
- **New**: `/sakin-core/services/network-sensor/`

### 2. Namespace Updates
- **Old**: `SAKINCore`, `SAKINCore.Handlers`, `SAKINCore.Utils`
- **New**: `Sakin.Core.Sensor`, `Sakin.Core.Sensor.Handlers`, `Sakin.Core.Sensor.Utils`, `Sakin.Core.Sensor.Services`, `Sakin.Core.Sensor.Configuration`

### 3. Dependency Injection

#### Old Pattern (Static Classes)
```csharp
var dbConnection = DatabaseHandler.InitDB();
DatabaseHandler.SavePacketAsync(dbConnection, ...);
```

#### New Pattern (DI with Interfaces)
```csharp
public class PackageInspector : IPackageInspector
{
    private readonly IDatabaseHandler _databaseHandler;

    public PackageInspector(IDatabaseHandler databaseHandler)
    {
        _databaseHandler = databaseHandler;
    }
}
```

### 4. Configuration Management

#### Old Pattern (Hardcoded)
```csharp
string connString = "Host=localhost;Username=postgres;Password=kaan1980;Database=network_db;Port=5432";
```

#### New Pattern (Configuration-based)
```csharp
// appsettings.json
{
  "Database": {
    "Host": "localhost",
    "Username": "postgres",
    "Password": "kaan1980",
    "Database": "network_db",
    "Port": 5432
  }
}

// Injected via IOptions<DatabaseOptions>
public DatabaseHandler(IOptions<DatabaseOptions> options)
{
    _options = options.Value;
    string connString = _options.GetConnectionString();
}
```

### 5. Application Lifecycle

#### Old Pattern (Console Main)
```csharp
static void Main(string[] args)
{
    var interfaces = CaptureDeviceList.Instance;
    var dbConnection = DatabaseHandler.InitDB();
    var wg = new ManualResetEvent(false);
    PackageInspector.MonitorTraffic(interfaces, dbConnection, wg);
    wg.WaitOne();
}
```

#### New Pattern (Host Builder with BackgroundService)
```csharp
static async Task Main(string[] args)
{
    var host = CreateHostBuilder(args).Build();
    await host.RunAsync();
}

static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            services.Configure<DatabaseOptions>(...);
            services.AddSingleton<IDatabaseHandler, DatabaseHandler>();
            services.AddSingleton<IPackageInspector, PackageInspector>();
            services.AddHostedService<NetworkSensorService>();
        });
```

## Migration Checklist

- [x] Create new directory structure
- [x] Update project file with .NET 8 target
- [x] Add DI packages (Microsoft.Extensions.Hosting, Configuration, etc.)
- [x] Convert static classes to injectable services
- [x] Add interfaces for testability
- [x] Implement configuration binding with DatabaseOptions
- [x] Create BackgroundService for lifecycle management
- [x] Update all namespaces
- [x] Copy appsettings.json to output directory
- [x] Preserve all existing functionality (PackageInspector, TLSParser)
- [x] Add project to solution

## Benefits of Migration

1. **Testability**: Interfaces enable unit testing with mock implementations
2. **Configuration**: External configuration without recompilation
3. **Maintainability**: Clear separation of concerns
4. **Modern Patterns**: Follows current .NET best practices
5. **Scalability**: Easy to add new services and handlers
6. **Lifecycle Management**: Proper startup/shutdown handling

## Running the Migrated Service

```bash
cd /sakin-core/services/network-sensor
dotnet run
```

Or with custom configuration:
```bash
export Database__Password="new_password"
dotnet run
```

## Backward Compatibility

The old SAKINCore-CS project remains in the repository for reference but should be considered deprecated. All new development should use the Sakin.Core.Sensor service.

## Future Enhancements

With the new architecture, the following enhancements are now easier to implement:

1. **Logging**: Add structured logging with ILogger<T>
2. **Metrics**: Add telemetry and monitoring
3. **Testing**: Add unit tests for handlers and services
4. **Multiple Databases**: Support different database providers
5. **Configuration Sources**: Azure Key Vault, environment-specific configs
6. **Health Checks**: Add health check endpoints
