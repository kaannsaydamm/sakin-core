# Sakin Ingest Worker Service

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . ./
RUN dotnet restore "sakin-ingest/Sakin.Ingest/Sakin.Ingest.csproj"
RUN dotnet publish "sakin-ingest/Sakin.Ingest/Sakin.Ingest.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Sakin.Ingest.dll"]
