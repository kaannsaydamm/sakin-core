FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder
WORKDIR /build

COPY . .
RUN dotnet restore
RUN dotnet build sakin-enrichment/Sakin.ThreatIntelService/Sakin.ThreatIntelService.csproj -c Release --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

COPY --from=builder /build/sakin-enrichment/Sakin.ThreatIntelService/bin/Release/net8.0 .

ENTRYPOINT ["dotnet", "Sakin.ThreatIntelService.dll"]
