using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartEVCharging.Application.DTOs.User;
using SmartEVCharging.Application.Interfaces;

namespace SmartEVCharging.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _users;
    public UserController(IUserService users) { _users = users; }

    /// <summary>Get profile by JWT claim (Android / Swagger).</summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        try { return Ok(await _users.GetProfileAsync(GetUserId())); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Deposit plastic bottles to earn points and charging credits.</summary>
    [HttpPost("deposit-bottles")]
    public async Task<IActionResult> DepositBottles([FromBody] DepositBottleRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _users.DepositBottlesAsync(GetUserId(), request)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ---------------------------------------------------------------
    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new InvalidOperationException("User ID claim missing."));
}
