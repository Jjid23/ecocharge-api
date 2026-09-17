using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartEVCharging.Application.DTOs.BinStatus;
using SmartEVCharging.Application.Interfaces;

namespace SmartEVCharging.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BinStatusController : ControllerBase
{
    private readonly IBinStatusService _bin;

    public BinStatusController(IBinStatusService bin)
    {
        _bin = bin;
    }

    /// <summary>
    /// Get the current bin fill level.
    /// Open to authenticated users and to the ESP32 device (no role restriction).
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(BinStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get()
    {
        var status = await _bin.GetBinStatusAsync();
        return Ok(status);
    }

    /// <summary>
    /// Update the bin fill level.
    /// Intended for the ESP32 kiosk device; restricted to Admin role in production.
    /// For development, the [AllowAnonymous] fallback lets the Android simulator push values.
    /// </summary>
    [HttpPut]
    [Authorize(Roles = "Admin,Device")]
    [ProducesResponseType(typeof(BinStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateBinStatusRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var status = await _bin.UpdateBinStatusAsync(request);
        return Ok(status);
    }
}
