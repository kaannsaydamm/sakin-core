# Sakin Utils

## Overview
Shared utilities, libraries, and common functionality used across Sakin platform services.

## Purpose
This directory will contain:
- Common data models and schemas
- Shared configuration management
- Utility functions and helpers
- Logging and metrics libraries
- Authentication/authorization libraries
- API client libraries
- Database migration scripts
- Testing utilities and mocks

## Status
🚧 **Placeholder** - This component is planned for future implementation.

## Planned Components

### Shared Libraries
- **sakin-common**: Core data models and interfaces
- **sakin-config**: Configuration management utilities
- **sakin-logging**: Structured logging wrappers
- **sakin-metrics**: Observability and metrics helpers
- **sakin-auth**: Authentication/authorization utilities

### Database
- **sakin-db-models**: Shared database models
- **migrations**: Database migration scripts (SQL/Flyway/EF Core)

### Testing
- **test-fixtures**: Shared test data and fixtures
- **mock-services**: Mock implementations for testing

## Architecture
Will use:
- NuGet packages for .NET shared libraries
- npm packages for JavaScript/TypeScript utilities
- Semantic versioning for library releases
- Centralized dependency management

## Integration
All Sakin services will depend on relevant utilities from this directory to avoid code duplication and ensure consistency.
