# ── Build .NET API ────────────────────────────────────────────────────────────
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

# ── Runtime: Debian base with both .NET runtime and Python ────────────────────
FROM debian:bookworm-slim AS runtime
WORKDIR /app

# Install .NET 9 runtime, Python 3, pip, supervisor, and system libs
RUN apt-get update && apt-get install -y --no-install-recommends \
    wget curl ca-certificates \
    python3 python3-pip python3-venv \
    libglib2.0-0 libsm6 libxext6 libxrender-dev libgl1 \
    supervisor \
    && rm -rf /var/lib/apt/lists/*

# Install .NET 9 ASP.NET runtime
RUN wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh \
    && chmod +x /tmp/dotnet-install.sh \
    && /tmp/dotnet-install.sh --runtime aspnetcore --version 9.0.0 --install-dir /usr/share/dotnet \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
    && rm /tmp/dotnet-install.sh

# Install Python YOLO dependencies
COPY yolo-requirements.txt /yolo-requirements.txt
RUN pip3 install --no-cache-dir -r /yolo-requirements.txt

# Copy YOLO server + model
COPY yolo-server/main.py /yolo/main.py
COPY yolo-server/v2.pt   /yolo/v2.pt

# Copy .NET publish output
COPY --from=build /app/publish .

# Supervisor config
COPY supervisord.conf /etc/supervisor/conf.d/supervisord.conf

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
ENV DOTNET_ROOT=/usr/share/dotnet

EXPOSE 8080

CMD ["/usr/bin/supervisord", "-n", "-c", "/etc/supervisor/conf.d/supervisord.conf"]
