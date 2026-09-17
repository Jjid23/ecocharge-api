# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY SmartEVCharging.sln ./
COPY src/SmartEVCharging.Domain/SmartEVCharging.Domain.csproj             src/SmartEVCharging.Domain/
COPY src/SmartEVCharging.Application/SmartEVCharging.Application.csproj   src/SmartEVCharging.Application/
COPY src/SmartEVCharging.Infrastructure/SmartEVCharging.Infrastructure.csproj src/SmartEVCharging.Infrastructure/
COPY src/SmartEVCharging.API/SmartEVCharging.API.csproj                    src/SmartEVCharging.API/
RUN dotnet restore

COPY . .
RUN dotnet publish src/SmartEVCharging.API/SmartEVCharging.API.csproj \
    -c Release -o /app/publish --no-restore

# ── Runtime stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production

# Use shell form so $PORT is expanded at container runtime (Railway injects PORT=8080)
CMD ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet SmartEVCharging.API.dll
