# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and restore
COPY SmartEVCharging.sln ./
COPY src/SmartEVCharging.Domain/SmartEVCharging.Domain.csproj             src/SmartEVCharging.Domain/
COPY src/SmartEVCharging.Application/SmartEVCharging.Application.csproj   src/SmartEVCharging.Application/
COPY src/SmartEVCharging.Infrastructure/SmartEVCharging.Infrastructure.csproj src/SmartEVCharging.Infrastructure/
COPY src/SmartEVCharging.API/SmartEVCharging.API.csproj                    src/SmartEVCharging.API/
RUN dotnet restore

# Copy everything and publish
COPY . .
RUN dotnet publish src/SmartEVCharging.API/SmartEVCharging.API.csproj \
    -c Release -o /app/publish --no-restore

# ── Runtime stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Railway injects PORT automatically
ENV ASPNETCORE_URLS=http://+:${PORT:-5225}
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5225
ENTRYPOINT ["dotnet", "SmartEVCharging.API.dll"]
