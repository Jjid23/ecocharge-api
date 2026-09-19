using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SmartEVCharging.API.Services;

/// <summary>
/// Background service that pings the YOLO server every 10 minutes
/// to prevent Render free-tier from putting it to sleep.
/// </summary>
public class YoloKeepAliveService : BackgroundService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration     _config;
    private readonly ILogger<YoloKeepAliveService> _logger;

    public YoloKeepAliveService(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<YoloKeepAliveService> logger)
    {
        _httpFactory = httpFactory;
        _config      = config;
        _logger      = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var yoloBase = _config["Yolo:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:8000";

        _logger.LogInformation("[YoloKeepAlive] Starting — will ping {Url}/health every 10 minutes.", yoloBase);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);

            try
            {
                var http = _httpFactory.CreateClient("yolo");
                var res  = await http.GetAsync("/health", stoppingToken);
                _logger.LogInformation("[YoloKeepAlive] Ping → HTTP {Status}", (int)res.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[YoloKeepAlive] Ping failed: {Message}", ex.Message);
            }
        }
    }
}
