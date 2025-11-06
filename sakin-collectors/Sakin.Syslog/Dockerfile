FROM mcr.microsoft.com/dotnet/8.0-runtime AS base
WORKDIR /app
EXPOSE 514/udp
EXPOSE 514

FROM mcr.microsoft.com/dotnet/8.0-sdk AS build
WORKDIR /src
COPY ["Sakin.Syslog.csproj", "."]
RUN dotnet restore "./Sakin.Syslog.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "Sakin.Syslog.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sakin.Syslog.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sakin.Syslog.dll"]