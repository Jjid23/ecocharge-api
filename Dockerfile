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

# Railway provides PORT env var — ASP.NET Core reads ASPNETCORE_HTTP_PORTS directly
ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartEVCharging.API.dll"]
