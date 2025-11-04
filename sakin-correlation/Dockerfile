# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY ["sakin-correlation/Sakin.Correlation/Sakin.Correlation.csproj", "sakin-correlation/Sakin.Correlation/"]
COPY ["sakin-utils/Sakin.Common/Sakin.Common.csproj", "sakin-utils/Sakin.Common/"]
COPY ["sakin-utils/Sakin.Messaging/Sakin.Messaging.csproj", "sakin-utils/Sakin.Messaging/"]

RUN dotnet restore "sakin-correlation/Sakin.Correlation/Sakin.Correlation.csproj"

COPY sakin-correlation/Sakin.Correlation/ sakin-correlation/Sakin.Correlation/
COPY sakin-utils/Sakin.Common/ sakin-utils/Sakin.Common/
COPY sakin-utils/Sakin.Messaging/ sakin-utils/Sakin.Messaging/

WORKDIR /src/sakin-correlation/Sakin.Correlation
RUN dotnet build "Sakin.Correlation.csproj" -c Release -o /app/build
RUN dotnet publish "Sakin.Correlation.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS runtime
WORKDIR /app

RUN apk add --no-cache ca-certificates tzdata

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

ENTRYPOINT ["dotnet", "Sakin.Correlation.dll"]
