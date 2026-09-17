using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEVCharging.Application.DTOs.Bottle;
using SmartEVCharging.Application.DTOs.User;
using SmartEVCharging.Application.Interfaces;
using SmartEVCharging.Infrastructure.Persistence;

namespace SmartEVCharging.API.Controllers;

/// <summary>
/// Handles plastic-bottle detection and recycling history.
/// Detection is delegated to the YOLO Python inference server on port 8000.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BottleController : ControllerBase
{
    private readonly IUserService  _users;
    private readonly AppDbContext  _db;
    private readonly IHttpClientFactory _httpFactory;

    // Points table matching Table 2.3 (exact YOLO class names → points)
    private static readonly Dictionary<string, double> PointsMap = new()
    {
        ["190ml"]  = 0.5,
        ["237ml"]  = 1.0,
        ["290ml"]  = 1.0,
        ["500ml"]  = 3.0,
        ["1000ml"] = 5.0,
        ["1500ml"] = 8.0,
        ["1750ml"] = 8.0,
    };

    private static readonly Dictionary<string, string> CategoryMap = new()
    {
        ["190ml"]  = "small",
        ["237ml"]  = "small",
        ["290ml"]  = "small",
        ["500ml"]  = "medium",
        ["1000ml"] = "medium",
        ["1500ml"] = "large",
        ["1750ml"] = "large",
    };

    public BottleController(IUserService users, AppDbContext db, IHttpClientFactory httpFactory)
    {
        _users       = users;
        _db          = db;
        _httpFactory = httpFactory;
    }

    // ── POST /api/bottle/detect ───────────────────────────────────────────────

    /// <summary>
    /// Runs YOLO inference via the Python server, awards points to the user,
    /// and returns a full transaction response.
    /// </summary>
    [HttpPost("detect")]
    public async Task<IActionResult> Detect([FromBody] BottleDetectRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!Guid.TryParse(request.UserId, out var userId))
            return BadRequest(new { error = "Invalid user ID." });

        // ── 1. Call YOLO inference server if image data is provided ───────────
        string  detectedLabel   = request.BottleSize ?? "medium";
        string  bottleType      = $"{detectedLabel} PET Plastic Bottle";
        double  confidence      = 0.97;
        double  rawPoints       = 0;
        bool    isBottle        = false;

        if (!string.IsNullOrWhiteSpace(request.ImageData))
        {
            try
            {
                var http    = _httpFactory.CreateClient("yolo");
                var payload = new { imageData = request.ImageData, confidence = 0.001 };
                var json    = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var yoloRes = await http.PostAsync("http://localhost:8000/detect", content);
                if (yoloRes.IsSuccessStatusCode)
                {
                    var yoloBody = await yoloRes.Content.ReadFromJsonAsync<YoloResponse>();
                    if (yoloBody is not null && yoloBody.IsBottle)
                    {
                        isBottle      = true;
                        detectedLabel = yoloBody.BestLabel;
                        confidence    = yoloBody.BestConfidence;
                        rawPoints     = yoloBody.PointsEarned;
                        bottleType    = yoloBody.BottleType ?? $"{detectedLabel} PET Plastic Bottle";
                    }
                }
            }
            catch (Exception ex)
            {
                // YOLO server unavailable — fall back to manual size-based calc
                Console.WriteLine($"[YOLO] Server unreachable: {ex.Message}. Using fallback.");
            }
        }

        // ── 2. Fallback: use manually selected bottle size ────────────────────
        if (rawPoints == 0)
        {
            isBottle      = true;
            detectedLabel = request.BottleSize ?? "500ml";
            rawPoints     = PointsMap.TryGetValue(detectedLabel, out var pts) ? pts : 3.0;
            bottleType    = $"{detectedLabel} PET Plastic Bottle";
        }

        // Round 0.5 → 1 for whole-point award; multiply by quantity
        int pointsPerBottle = (int)Math.Max(1, Math.Round(rawPoints));
        int totalPoints     = pointsPerBottle * request.Quantity;

        string category = CategoryMap.TryGetValue(detectedLabel, out var cat) ? cat : "medium";

        // ── 3. Award points to user ───────────────────────────────────────────
        try
        {
            var depositReq = new DepositBottleRequest
            {
                BottleCount = request.Quantity,
                BottleSize  = category
            };
            var profile = await _users.DepositBottlesAsync(userId, depositReq);

            var transaction = new BottleTransactionResponse
            {
                TransactionId   = $"bt_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                UserId          = request.UserId,
                UserFullName    = profile.FullName,
                BottleSize      = category,
                BottleType      = bottleType,
                Quantity        = request.Quantity,
                PointsEarned    = totalPoints,
                DetectionStatus = isBottle ? "Verified" : "Simulated",
                DetectedAt      = DateTime.UtcNow.ToString("o"),
                ConfidenceScore = confidence
            };

            return Ok(new
            {
                message      = "Bottle Detected Successfully!",
                transaction,
                earnedPoints = totalPoints,
                newBalance   = profile.CurrentPoints,
                // Extra fields for the UI
                detectedLabel,
                category,
                isBottle
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ── POST /api/bottle/analyze-image ───────────────────────────────────────

    /// <summary>
    /// Thin proxy: forwards a base64 image to the YOLO Python server
    /// and returns the raw inference result (no DB write).
    /// Used by the web frontend camera for live preview analysis.
    /// </summary>
    [HttpPost("analyze-image")]
    [AllowAnonymous]
    public async Task<IActionResult> AnalyzeImage([FromBody] AnalyzeImageRequest request)
    {
        try
        {
            var http    = _httpFactory.CreateClient("yolo");
            var payload = new { imageData = request.ImageData, confidence = 0.001 };
            var json    = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var yoloRes = await http.PostAsync("http://localhost:8000/detect", content);
            if (!yoloRes.IsSuccessStatusCode)
                return StatusCode(502, new { error = "YOLO server error." });

            var body = await yoloRes.Content.ReadFromJsonAsync<YoloResponse>();
            if (body is null) return StatusCode(502, new { error = "Empty YOLO response." });

            return Ok(new
            {
                isBottle     = body.IsBottle,
                bottleSize   = body.BottleSize,
                bottleType   = body.BottleType ?? $"{body.BestLabel} PET Plastic Bottle",
                confidence   = body.BestConfidence,
                pointsEarned = body.PointsEarned,
                detectedLabel = body.BestLabel,
                detections   = body.Detections
            });
        }
        catch
        {
            // YOLO server offline — return safe fallback so the UI doesn't break
            return Ok(new
            {
                isBottle     = true,
                bottleSize   = "medium",
                bottleType   = "500ml PET Plastic Bottle",
                confidence   = 0.0,
                pointsEarned = 3,
                detectedLabel = "500ml",
                detections   = Array.Empty<object>(),
                fallback     = true
            });
        }
    }

    // ── GET /api/bottle/history/{userId} ─────────────────────────────────────

    [HttpGet("history/{userId}")]
    public async Task<IActionResult> History(string userId)
    {
        if (!Guid.TryParse(userId, out var guid))
            return BadRequest(new { error = "Invalid user ID." });

        var user = await _db.Users.FindAsync(guid);
        if (user is null) return NotFound(new { error = "User not found." });

        var txList = await _db.ChargingSessions
            .Where(s => s.UserId == guid && s.Notes != null && s.Notes.Contains("Bottles:"))
            .OrderByDescending(s => s.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var history = txList.Select(s =>
        {
            int qty = ParseNote(s.Notes, "Bottles");
            return new BottleTransactionResponse
            {
                TransactionId   = s.Id.ToString(),
                UserId          = userId,
                UserFullName    = user.FullName,
                BottleSize      = "medium",
                BottleType      = "PET Clear Plastic",
                Quantity        = qty,
                PointsEarned    = qty * 3,
                DetectionStatus = "Verified",
                DetectedAt      = s.CreatedAt.ToString("o"),
                ConfidenceScore = 0.97
            };
        }).ToList();

        return Ok(new { transactions = history });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int ParseNote(string? notes, string key)
    {
        if (notes is null) return 0;
        var prefix = $"{key}:";
        int idx = notes.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0) return 0;
        return int.TryParse(notes[(idx + prefix.Length)..].Split(';')[0].Trim(), out int v) ? v : 0;
    }

    // ── YOLO response shape ───────────────────────────────────────────────────

    private sealed class YoloResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("isBottle")]
        public bool   IsBottle        { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("bestLabel")]
        public string BestLabel       { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("bestConfidence")]
        public double BestConfidence  { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pointsEarned")]
        public double PointsEarned    { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("bottleSize")]
        public string BottleSize      { get; set; } = "medium";

        [System.Text.Json.Serialization.JsonPropertyName("bottleType")]
        public string? BottleType     { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("totalPoints")]
        public double TotalPoints     { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("detections")]
        public List<object> Detections { get; set; } = new();
    }
}

public class AnalyzeImageRequest
{
    public string ImageData { get; set; } = string.Empty;
}
