using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartEVCharging.Application.DTOs.Auth;
using SmartEVCharging.Application.Interfaces;

namespace SmartEVCharging.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IUserService _users;

    public AuthController(IAuthService auth, IUserService users)
    {
        _auth  = auth;
        _users = users;
    }

    /// <summary>Register a new user account.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var response = await _auth.RegisterAsync(request);
            return StatusCode(StatusCodes.Status201Created, new { message = "Registration successful!", user = response, token = response.Token });
        }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    /// <summary>Login — accepts username or email in the identifier field.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] WebLoginRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            // Map web identifier field → our LoginRequest
            var loginReq = new LoginRequest
            {
                Username = request.Identifier ?? request.Username ?? string.Empty,
                Password = request.Password
            };
            var response = await _auth.LoginAsync(loginReq);
            return Ok(new { message = "Login successful", user = response, token = response.Token });
        }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { error = ex.Message }); }
    }

    /// <summary>Refresh current user data by userId (web frontend polling).</summary>
    [HttpGet("me/{userId}")]
    [Authorize]
    public async Task<IActionResult> Me(string userId)
    {
        if (!Guid.TryParse(userId, out var guid))
            return BadRequest(new { error = "Invalid user ID." });
        try
        {
            var profile = await _users.GetProfileAsync(guid);
            return Ok(new { user = profile });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>Update profile — fullName, username, email, password.</summary>
    [HttpPut("profile/{userId}")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile(string userId, [FromBody] Application.DTOs.User.UpdateProfileRequest request)
    {
        if (!Guid.TryParse(userId, out var guid))
            return BadRequest(new { error = "Invalid user ID." });
        try
        {
            var profile = await _users.UpdateProfileAsync(guid, request);
            return Ok(new { message = "Profile updated successfully!", user = profile });
        }
        catch (KeyNotFoundException ex)      { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

/// <summary>Accepts both web (identifier) and Android (username) login formats.</summary>
public class WebLoginRequest
{
    public string? Identifier { get; set; }  // web: email or username
    public string? Username   { get; set; }  // Android: username only
    public string  Password   { get; set; } = string.Empty;
}
