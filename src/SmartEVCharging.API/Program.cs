using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartEVCharging.Application.Interfaces;
using SmartEVCharging.Infrastructure.Persistence;
using SmartEVCharging.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Kestrel ────────────────────────────────────────────────────────────────
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MinRequestBodyDataRate = null;
    options.Limits.MinResponseDataRate    = null;
});

// ── Database ───────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── JWT Authentication ─────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// ── CORS — reads allowed origins from env var in production ────────────────
var allowedOrigins = builder.Configuration["AllowedOrigins"]
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? Array.Empty<string>();

// Always include local dev origins
var allOrigins = new List<string>
{
    "http://10.0.2.2:5225",
    "http://localhost:5225",
    "https://localhost:7225",
    "http://localhost:3000",
    "http://127.0.0.1:3000"
};
allOrigins.AddRange(allowedOrigins);

builder.Services.AddCors(options =>
{
    options.AddPolicy("EcoChargePolicy", policy =>
        policy.WithOrigins(allOrigins.Distinct().ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ── Application / Infrastructure services ─────────────────────────────────
builder.Services.AddScoped<IJwtService,             JwtService>();
builder.Services.AddScoped<IAuthService,            AuthService>();
builder.Services.AddScoped<IUserService,            UserService>();
builder.Services.AddScoped<IChargingPortService,    ChargingPortService>();
builder.Services.AddScoped<IChargingSessionService, ChargingSessionService>();
builder.Services.AddScoped<IBinStatusService,       BinStatusService>();

// ── HTTP clients ───────────────────────────────────────────────────────────
// YOLO base URL: env var in production, localhost in dev
var yoloBaseUrl = builder.Configuration["Yolo:BaseUrl"] ?? "http://localhost:8000/";
builder.Services.AddHttpClient("yolo", client =>
{
    client.BaseAddress = new Uri(yoloBaseUrl.TrimEnd('/') + "/");
    client.Timeout     = TimeSpan.FromSeconds(30);
});

// ── Controllers ────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── Swagger ────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "Smart EV Charging API",
        Version     = "v1",
        Description = "EcoCharge backend — mobile app, web, kiosk & ESP32."
    });
    var sec = new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Description  = "Bearer {token}",
        In           = ParameterLocation.Header,
        Type         = SecuritySchemeType.ApiKey,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        Reference    = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = JwtBearerDefaults.AuthenticationScheme }
    };
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, sec);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { { sec, Array.Empty<string>() } });
});

// ── Build ──────────────────────────────────────────────────────────────────
var app = builder.Build();

// Always show Swagger (useful on Railway too)
app.UseSwagger();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "Smart EV Charging API v1"));

// Auto-migrate on startup (works locally and on Railway)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseCors("EcoChargePolicy");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
