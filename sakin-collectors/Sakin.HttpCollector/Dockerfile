FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["sakin-collectors/Sakin.HttpCollector/Sakin.HttpCollector.csproj", "sakin-collectors/Sakin.HttpCollector/"]
COPY ["sakin-utils/Sakin.Common/Sakin.Common.csproj", "sakin-utils/Sakin.Common/"]
COPY ["sakin-utils/Sakin.Messaging/Sakin.Messaging.csproj", "sakin-utils/Sakin.Messaging/"]
RUN dotnet restore "sakin-collectors/Sakin.HttpCollector/Sakin.HttpCollector.csproj"

COPY . .
WORKDIR "/src/sakin-collectors/Sakin.HttpCollector"
RUN dotnet build "Sakin.HttpCollector.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sakin.HttpCollector.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sakin.HttpCollector.dll"]
