# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Base runtime image — shared by the build's final stage.
#  - tzdata: the Debian-slim base ships no IANA zone database, so without it
#    .NET's TimeZoneInfo.Local (and Serilog's local timestamps) fall back to UTC.
#    Installing it + TZ=Asia/Dhaka makes log timestamps and daily file rolls
#    use Dhaka (UTC+6) local time.
#  - curl: lets the container self-report health (used by an optional HEALTHCHECK).
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    TZ=Asia/Dhaka
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl tzdata \
    && rm -rf /var/lib/apt/lists/*

# ---------------------------------------------------------------------------
# Build + publish in one SDK stage. Restore is cached on the csproj/props layer
# so source-only changes skip the package restore. publish compiles too, so a
# separate build step is redundant.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/SalesCom.Domain/SalesCom.Domain.csproj", "src/SalesCom.Domain/"]
COPY ["src/SalesCom.Application/SalesCom.Application.csproj", "src/SalesCom.Application/"]
COPY ["src/SalesCom.Infrastructure/SalesCom.Infrastructure.csproj", "src/SalesCom.Infrastructure/"]
COPY ["src/SalesCom.Api/SalesCom.Api.csproj", "src/SalesCom.Api/"]
RUN dotnet restore "src/SalesCom.Api/SalesCom.Api.csproj"

COPY . .
RUN dotnet publish "src/SalesCom.Api/SalesCom.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

# ---------------------------------------------------------------------------
# Final image — non-root, minimal.
# ---------------------------------------------------------------------------
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Serilog writes rolling files to /app/logs; create it owned by the non-root app
# user so the file sink can write (a volume mounted here inherits this on first run).
RUN mkdir -p /app/logs && chown -R $APP_UID /app/logs
USER $APP_UID
ENTRYPOINT ["dotnet", "SalesCom.Api.dll"]
