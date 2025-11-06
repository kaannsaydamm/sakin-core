FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ["Sakin.Agent.Linux.csproj", "."]
RUN dotnet restore "./Sakin.Agent.Linux.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "Sakin.Agent.Linux.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sakin.Agent.Linux.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Install required system packages for iptables support
RUN apk add --no-cache \
    iptables \
    ip6tables \
    iptables-doc \
    bash \
    coreutils

# Create non-root user
RUN addgroup -g 1001 sakin-agent && \
    adduser -D -s /bin/sh -u 1001 -G sakin-agent sakin-agent

# Create directories
RUN mkdir -p /app/scripts /app/logs && \
    chown -R sakin-agent:sakin-agent /app

COPY --from=publish /app/publish .

# Set proper permissions
RUN chmod +x /app/Sakin.Agent.Linux.dll

USER sakin-agent

ENTRYPOINT ["dotnet", "Sakin.Agent.Linux.dll"]