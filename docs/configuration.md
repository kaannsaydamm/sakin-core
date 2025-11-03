# Configuration Guide

## Overview

This guide describes the configuration hierarchy, patterns, and best practices used across all Sakin platform services. All services follow .NET configuration conventions with support for environment variables, user secrets, and multiple configuration files.

## Configuration Hierarchy

.NET services use a layered configuration approach where later sources override earlier ones:

1. **appsettings.json** - Base configuration (committed to source control)
2. **appsettings.{Environment}.json** - Environment-specific overrides (Development, Staging, Production)
3. **User Secrets** - Local development secrets (Development only, not committed)
4. **Environment Variables** - Runtime configuration (all environments)
5. **Command Line Arguments** - Highest priority overrides (optional)

### Priority Example

If a database password is defined in multiple places:
- `appsettings.json`: `"Password": "default"`
- `appsettings.Development.json`: `"Password": "dev_password"`
- Environment Variable: `Database__Password=env_password`
- User Secrets: `"Database:Password": "secret_password"`

The final value used will be: **`secret_password`** (User Secrets has highest priority in Development)

## Configuration Files

### appsettings.json

Base configuration file that contains:
- Non-sensitive default values
- Configuration structure and schema
- Logging defaults
- Feature flags

**✅ Safe to commit to source control**

### appsettings.Development.json

Development environment overrides:
- Local database connections
- Verbose logging for debugging
- Development-specific endpoints
- TODO placeholders for values to be set via User Secrets

**✅ Safe to commit with TODO placeholders**

### appsettings.Production.json

Production environment overrides:
- Production logging levels (Warning/Error)
- Performance optimizations
- TODO placeholders only (actual values via environment variables)

**✅ Safe to commit with TODO placeholders**

## Environment Variables

Environment variables use a double-underscore (`__`) separator to represent nested configuration:

```bash
# Configuration hierarchy: Database → Password
export Database__Password="my_secure_password"

# Configuration hierarchy: Logging → LogLevel → Default
export Logging__LogLevel__Default="Warning"

# Configuration hierarchy: MessageBroker → RabbitMQ → Host
export MessageBroker__RabbitMQ__Host="rabbitmq.production.local"
```

### Docker / Kubernetes

Environment variables are the recommended approach for containerized deployments:

```yaml
# docker-compose.yml
environment:
  - Database__Host=postgres
  - Database__Username=sakin_user
  - Database__Password=${DB_PASSWORD}
  - Logging__LogLevel__Default=Information
```

```yaml
# Kubernetes ConfigMap / Secret
apiVersion: v1
kind: Secret
metadata:
  name: sakin-sensor-config
stringData:
  Database__Password: "production_password"
  Database__Host: "postgres.database.svc.cluster.local"
```

## User Secrets (Development Only)

User Secrets provide secure local development without committing credentials.

### Setup

1. **Add UserSecretsId to .csproj**:
```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <UserSecretsId>sakin-network-sensor-12345</UserSecretsId>
</PropertyGroup>
```

2. **Initialize User Secrets**:
```bash
cd sakin-core/services/network-sensor
dotnet user-secrets init
```

3. **Set Secret Values**:
```bash
# Set database password
dotnet user-secrets set "Database:Password" "local_dev_password"

# Set entire configuration section
dotnet user-secrets set "Database:Host" "localhost"
dotnet user-secrets set "Database:Username" "postgres"
dotnet user-secrets set "Database:Database" "network_db"
dotnet user-secrets set "Database:Port" "5432"
```

4. **List All Secrets**:
```bash
dotnet user-secrets list
```

5. **Remove Secrets**:
```bash
dotnet user-secrets remove "Database:Password"
dotnet user-secrets clear  # Remove all secrets
```

### User Secrets Location

Secrets are stored outside the project directory:
- **Windows**: `%APPDATA%\Microsoft\UserSecrets\<user_secrets_id>\secrets.json`
- **Linux/macOS**: `~/.microsoft/usersecrets/<user_secrets_id>/secrets.json`

## Shared Configuration Models

All services use the `Sakin.Common` library for shared configuration models:

### DatabaseOptions

Standard database configuration used across all services:

```csharp
using Sakin.Common.Configuration;

// In Program.cs
services.Configure<DatabaseOptions>(
    context.Configuration.GetSection(DatabaseOptions.SectionName));

// In service constructor
public class DatabaseHandler : IDatabaseHandler
{
    private readonly DatabaseOptions _options;
    
    public DatabaseHandler(IOptions<DatabaseOptions> options)
    {
        _options = options.Value;
    }
    
    public void Connect()
    {
        string connectionString = _options.GetConnectionString();
        // Use connection string
    }
}
```

**Configuration Section**: `"Database"`

**Properties**:
- `Host` (string): Database server hostname
- `Username` (string): Database username
- `Password` (string): Database password
- `Database` (string): Database name
- `Port` (int): Database port (default: 5432)

## Service Configuration Examples

### Network Sensor Service

The network sensor requires database configuration and optional packet capture settings.

**appsettings.json** (base):
```json
{
  "Database": {
    "Host": "localhost",
    "Username": "postgres",
    "Password": "TODO_SET_VIA_USER_SECRETS",
    "Database": "network_db",
    "Port": 5432
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

**appsettings.Development.json**:
```json
{
  "Database": {
    "Host": "localhost",
    "Username": "postgres",
    "Password": "TODO_SET_VIA_USER_SECRETS",
    "Database": "network_db_dev",
    "Port": 5432
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Sakin.Core.Sensor": "Trace",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

**User Secrets** (Development):
```bash
cd sakin-core/services/network-sensor
dotnet user-secrets set "Database:Password" "your_local_password"
```

**Environment Variables** (Production):
```bash
export Database__Host="postgres.production.local"
export Database__Username="sensor_user"
export Database__Password="$(cat /run/secrets/db_password)"
export Database__Database="network_db"
export Logging__LogLevel__Default="Warning"
```

### Ingest Service (Planned)

The ingest service will require database, message broker, and data processing configuration.

**appsettings.json** (base):
```json
{
  "Database": {
    "Host": "localhost",
    "Username": "postgres",
    "Password": "TODO_SET_VIA_USER_SECRETS",
    "Database": "ingest_db",
    "Port": 5432
  },
  "MessageBroker": {
    "Type": "RabbitMQ",
    "RabbitMQ": {
      "Host": "localhost",
      "Port": 5672,
      "Username": "guest",
      "Password": "TODO_SET_VIA_USER_SECRETS",
      "VirtualHost": "/",
      "Exchange": "sakin.events",
      "Queue": "ingest.queue"
    }
  },
  "Ingestion": {
    "BatchSize": 100,
    "FlushIntervalSeconds": 30,
    "MaxRetries": 3,
    "EnableDeduplication": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

**appsettings.Development.json**:
```json
{
  "Database": {
    "Database": "ingest_db_dev"
  },
  "MessageBroker": {
    "RabbitMQ": {
      "Host": "localhost"
    }
  },
  "Ingestion": {
    "BatchSize": 10
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Sakin.Ingest": "Trace"
    }
  }
}
```

**User Secrets** (Development):
```bash
cd sakin-ingest
dotnet user-secrets set "Database:Password" "local_password"
dotnet user-secrets set "MessageBroker:RabbitMQ:Password" "guest"
```

**Environment Variables** (Production):
```bash
export Database__Password="$(cat /run/secrets/db_password)"
export MessageBroker__RabbitMQ__Host="rabbitmq.service.consul"
export MessageBroker__RabbitMQ__Password="$(cat /run/secrets/rabbitmq_password)"
export Ingestion__BatchSize="1000"
export Logging__LogLevel__Default="Warning"
```

### Correlation Service (Planned)

The correlation engine requires database, message broker, and correlation rule configuration.

**appsettings.json** (base):
```json
{
  "Database": {
    "Host": "localhost",
    "Username": "postgres",
    "Password": "TODO_SET_VIA_USER_SECRETS",
    "Database": "correlation_db",
    "Port": 5432
  },
  "MessageBroker": {
    "Type": "RabbitMQ",
    "RabbitMQ": {
      "Host": "localhost",
      "Port": 5672,
      "Username": "guest",
      "Password": "TODO_SET_VIA_USER_SECRETS",
      "VirtualHost": "/",
      "Exchange": "sakin.events",
      "Queue": "correlation.queue"
    }
  },
  "Correlation": {
    "RulesDirectory": "./rules",
    "TimeWindowSeconds": 300,
    "MaxEventsInWindow": 10000,
    "EnableMachineLearning": false,
    "AlertThreshold": 0.8
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

**appsettings.Development.json**:
```json
{
  "Database": {
    "Database": "correlation_db_dev"
  },
  "MessageBroker": {
    "RabbitMQ": {
      "Host": "localhost"
    }
  },
  "Correlation": {
    "RulesDirectory": "../../../test-rules",
    "TimeWindowSeconds": 60,
    "MaxEventsInWindow": 100
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Sakin.Correlation": "Trace"
    }
  }
}
```

**User Secrets** (Development):
```bash
cd sakin-correlation
dotnet user-secrets set "Database:Password" "local_password"
dotnet user-secrets set "MessageBroker:RabbitMQ:Password" "guest"
```

**Environment Variables** (Production):
```bash
export Database__Password="$(cat /run/secrets/db_password)"
export MessageBroker__RabbitMQ__Host="rabbitmq.service.consul"
export MessageBroker__RabbitMQ__Password="$(cat /run/secrets/rabbitmq_password)"
export Correlation__RulesDirectory="/etc/sakin/rules"
export Correlation__EnableMachineLearning="true"
export Logging__LogLevel__Default="Warning"
```

## Configuration Validation

All services should validate configuration at startup using the Options pattern:

```csharp
services.AddOptions<DatabaseOptions>()
    .Bind(configuration.GetSection(DatabaseOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

Add validation attributes to configuration classes:

```csharp
using System.ComponentModel.DataAnnotations;

public class DatabaseOptions
{
    [Required]
    public string Host { get; set; } = "localhost";
    
    [Required]
    public string Username { get; set; } = "postgres";
    
    [Required]
    public string Password { get; set; } = string.Empty;
    
    [Required]
    public string Database { get; set; } = "network_db";
    
    [Range(1, 65535)]
    public int Port { get; set; } = 5432;
}
```

## Security Best Practices

### ✅ DO

- Use User Secrets for local development credentials
- Use environment variables for production secrets
- Store production secrets in secure vaults (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault)
- Commit appsettings.*.json files with TODO placeholders
- Use different database credentials per environment
- Rotate credentials regularly
- Use least-privilege database accounts

### ❌ DON'T

- Commit actual passwords or secrets to source control
- Use default passwords in production
- Share credentials between environments
- Log configuration values that contain secrets
- Store secrets in Kubernetes ConfigMaps (use Secrets instead)
- Use the same UserSecretsId across multiple services

## Troubleshooting

### Configuration Not Loading

1. Check environment variable syntax (double underscore: `__`)
2. Verify `ASPNETCORE_ENVIRONMENT` or `DOTNET_ENVIRONMENT` is set correctly
3. Ensure appsettings.{Environment}.json exists if referenced
4. Check file paths in `AddJsonFile()` calls

### Secrets Not Working

1. Verify UserSecretsId exists in .csproj
2. Run `dotnet user-secrets list` to check values
3. Ensure `AddUserSecrets()` is called in Program.cs for Development
4. Check secrets file location: `~/.microsoft/usersecrets/<id>/secrets.json`

### Connection String Issues

1. Test environment variable expansion: `echo $Database__Password`
2. Verify `DatabaseOptions.GetConnectionString()` is used consistently
3. Check for special characters that need escaping in passwords
4. Ensure database service is accessible from application

## Testing Configuration

Test configuration binding without running the application:

```bash
# Create a test console app
dotnet new console -n ConfigTest
cd ConfigTest

# Add required packages
dotnet add package Microsoft.Extensions.Configuration
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package Microsoft.Extensions.Configuration.EnvironmentVariables
dotnet add package Microsoft.Extensions.Configuration.UserSecrets

# Create test program
cat > Program.cs << 'EOF'
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

Console.WriteLine("Configuration loaded successfully!");
Console.WriteLine($"Database Host: {configuration["Database:Host"]}");
Console.WriteLine($"Database Name: {configuration["Database:Database"]}");
Console.WriteLine($"Password Set: {!string.IsNullOrEmpty(configuration["Database:Password"])}");
EOF

# Copy appsettings.json to test
cp ../path/to/appsettings.json .
cp ../path/to/appsettings.Development.json .

# Run test
dotnet run
```

## Reference

- [Configuration in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [Safe storage of app secrets in development](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [Options pattern in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options)
- [Environment variables in .NET](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables)
