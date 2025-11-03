# Configuration Samples

This document provides quick reference for the configuration samples available across the Sakin platform services.

## Available Configuration Files

### Network Sensor Service
Location: `sakin-core/services/network-sensor/`

- **appsettings.json** - Base configuration for all environments
- **appsettings.Development.json** - Development environment overrides with verbose logging

Configuration includes:
- Database connection settings (PostgreSQL)
- Logging levels for different namespaces
- User Secrets support enabled (UserSecretsId configured)

### Ingest Service (Planned)
Location: `sakin-ingest/`

- **appsettings.json** - Base configuration for ingestion pipeline
- **appsettings.Development.json** - Development environment with reduced batch sizes

Configuration includes:
- Database connection settings (PostgreSQL)
- Message broker configuration (RabbitMQ)
- Ingestion pipeline settings (batch size, deduplication, enrichment)
- Logging configuration

### Correlation Service (Planned)
Location: `sakin-correlation/`

- **appsettings.json** - Base configuration for correlation engine
- **appsettings.Development.json** - Development environment with reduced time windows

Configuration includes:
- Database connection settings (PostgreSQL)
- Message broker configuration (RabbitMQ)
- Correlation engine settings (time windows, ML, rules directory)
- Threat intelligence provider configuration
- Logging configuration

## Quick Start

### Setting Up a Service

1. **Copy configuration templates** (already in place):
   ```bash
   cd sakin-core/services/network-sensor
   # Files already present: appsettings.json, appsettings.Development.json
   ```

2. **Set up User Secrets for local development**:
   ```bash
   # Initialize user secrets (already configured)
   dotnet user-secrets list  # Verify UserSecretsId is configured
   
   # Set database password
   dotnet user-secrets set "Database:Password" "your_local_password"
   
   # Optionally override other settings
   dotnet user-secrets set "Database:Host" "localhost"
   ```

3. **Run in Development mode**:
   ```bash
   export DOTNET_ENVIRONMENT=Development
   # or
   export ASPNETCORE_ENVIRONMENT=Development
   
   dotnet run
   ```

4. **Override via Environment Variables** (alternative to User Secrets):
   ```bash
   export Database__Password="your_password"
   export Database__Host="localhost"
   dotnet run
   ```

### Production Deployment

For production, use environment variables instead of User Secrets:

```bash
# Set environment
export DOTNET_ENVIRONMENT=Production

# Configure via environment variables
export Database__Host="postgres.production.local"
export Database__Username="sensor_user"
export Database__Password="$(cat /run/secrets/db_password)"
export Database__Database="network_db"
export Logging__LogLevel__Default="Warning"

# Run service
dotnet run
```

Or use a secrets management system:
- **Docker Secrets**: Mount secrets as files in `/run/secrets/`
- **Kubernetes Secrets**: Use environment variables from Secret resources
- **Azure Key Vault**: Use Key Vault configuration provider
- **AWS Secrets Manager**: Use AWS Secrets Manager configuration provider
- **HashiCorp Vault**: Use Vault configuration provider

## Configuration Validation

All configuration files have been validated to ensure:
- ✅ Valid JSON syntax
- ✅ Proper structure for .NET Configuration binding
- ✅ Database configuration uses `Sakin.Common.Configuration.DatabaseOptions`
- ✅ TODO placeholders for sensitive values
- ✅ Environment-specific overrides follow best practices

You can verify configuration loading using the test program in `/tmp/ConfigTest/`.

## Common Configuration Patterns

### Database Configuration
All services use the shared `DatabaseOptions` from `Sakin.Common`:

```json
{
  "Database": {
    "Host": "localhost",
    "Username": "postgres",
    "Password": "TODO_SET_VIA_USER_SECRETS_OR_ENV_VAR",
    "Database": "service_db",
    "Port": 5432
  }
}
```

### Logging Configuration
Standard logging configuration with namespace-specific levels:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Sakin.Service.Namespace": "Debug",
      "Microsoft.Hosting.Lifetime": "Information",
      "System": "Information"
    }
  }
}
```

### Message Broker Configuration (Future Services)
For services using message brokers:

```json
{
  "MessageBroker": {
    "Type": "RabbitMQ",
    "RabbitMQ": {
      "Host": "localhost",
      "Port": 5672,
      "Username": "guest",
      "Password": "TODO_SET_VIA_USER_SECRETS_OR_ENV_VAR",
      "VirtualHost": "/",
      "Exchange": "sakin.events",
      "Queue": "service.queue"
    }
  }
}
```

## Security Notes

- All committed configuration files use TODO placeholders for sensitive values
- User Secrets are configured but empty by default
- User Secrets are stored outside the repository in `~/.microsoft/usersecrets/`
- Production deployments should use environment variables or secrets management systems
- Never commit actual passwords or API keys to the repository

## See Also

- [Configuration Guide](configuration.md) - Comprehensive configuration documentation
- [Sakin.Common Library](../sakin-utils/Sakin.Common/README.md) - Shared configuration models
- [Network Sensor README](../sakin-core/services/network-sensor/README.md) - Service-specific documentation
