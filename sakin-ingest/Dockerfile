# Sakin Ingest Service - Dockerfile

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ["Sakin.Ingest.csproj", "./"]
COPY ["../sakin-utils/Sakin.Common/Sakin.Common.csproj", "../sakin-utils/Sakin.Common/"]
COPY ["../sakin-utils/Sakin.Messaging/Sakin.Messaging.csproj", "../sakin-utils/Sakin.Messaging/"]
RUN dotnet restore "./Sakin.Ingest.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "Sakin.Ingest.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sakin.Ingest.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sakin.Ingest.dll"]
