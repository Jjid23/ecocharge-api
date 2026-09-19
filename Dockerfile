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

# ── Runtime stage — .NET + Python YOLO in one container ───────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install Python 3 + pip + system libs for OpenCV/Pillow
RUN apt-get update && apt-get install -y --no-install-recommends \
    python3 python3-pip python3-venv \
    libglib2.0-0 libsm6 libxext6 libxrender-dev libgl1 \
    supervisor \
    && rm -rf /var/lib/apt/lists/*

# Install Python YOLO dependencies
COPY yolo-requirements.txt /yolo-requirements.txt
RUN pip3 install --no-cache-dir -r /yolo-requirements.txt

# Copy YOLO server files + model weights
COPY yolo-server/main.py  /yolo/main.py
COPY yolo-server/v2.pt    /yolo/v2.pt

# Copy .NET publish output
COPY --from=build /app/publish .

# Supervisor config: run both .NET API and Python YOLO server
COPY supervisord.conf /etc/supervisor/conf.d/supervisord.conf

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
ENV YOLO_PORT=8000

EXPOSE 8080

CMD ["/usr/bin/supervisord", "-c", "/etc/supervisor/conf.d/supervisord.conf"]
