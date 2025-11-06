FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["sakin-analytics/Sakin.Analytics.BaselineWorker/Sakin.Analytics.BaselineWorker.csproj", "sakin-analytics/Sakin.Analytics.BaselineWorker/"]
COPY ["sakin-utils/Sakin.Common/Sakin.Common.csproj", "sakin-utils/Sakin.Common/"]
COPY ["sakin-utils/Sakin.Messaging/Sakin.Messaging.csproj", "sakin-utils/Sakin.Messaging/"]
RUN dotnet restore "sakin-analytics/Sakin.Analytics.BaselineWorker/Sakin.Analytics.BaselineWorker.csproj"
COPY . .
WORKDIR "/src/sakin-analytics/Sakin.Analytics.BaselineWorker"
RUN dotnet build "Sakin.Analytics.BaselineWorker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sakin.Analytics.BaselineWorker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sakin.Analytics.BaselineWorker.dll"]
