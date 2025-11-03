# Sakin Network Sensor - Dockerfile
# Multi-stage build for optimized container size

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy solution and project files
COPY SAKINCore-CS.sln .
COPY sakin-utils/Sakin.Common/Sakin.Common.csproj sakin-utils/Sakin.Common/
COPY sakin-core/services/network-sensor/Sakin.Core.Sensor.csproj sakin-core/services/network-sensor/

# Restore dependencies
RUN dotnet restore sakin-core/services/network-sensor/Sakin.Core.Sensor.csproj

# Copy source code
COPY sakin-utils/Sakin.Common/ sakin-utils/Sakin.Common/
COPY sakin-core/services/network-sensor/ sakin-core/services/network-sensor/

# Build the application
WORKDIR /src/sakin-core/services/network-sensor
RUN dotnet build Sakin.Core.Sensor.csproj -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish Sakin.Core.Sensor.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS final

# Install required packages for packet capture
RUN apk add --no-cache \
    libpcap \
    libpcap-dev

WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Create non-root user (but note: packet capture requires NET_ADMIN capability)
RUN addgroup -g 1000 sakin && \
    adduser -D -u 1000 -G sakin sakin

# Environment variables with defaults
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Database__Host=postgres
ENV Database__Port=5432
ENV Database__Database=network_db
ENV Database__Username=postgres
ENV Database__Password=changeme

# Note: This service requires NET_ADMIN and NET_RAW capabilities
# and should be run with --cap-add=NET_ADMIN --cap-add=NET_RAW

ENTRYPOINT ["dotnet", "Sakin.Core.Sensor.dll"]
