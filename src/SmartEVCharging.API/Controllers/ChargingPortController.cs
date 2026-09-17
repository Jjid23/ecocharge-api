using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartEVCharging.Application.DTOs.ChargingPort;
using SmartEVCharging.Application.Interfaces;

namespace SmartEVCharging.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChargingPortController : ControllerBase
{
    private readonly IChargingPortService _ports;

    public ChargingPortController(IChargingPortService ports)
    {
        _ports = ports;
    }

    /// <summary>List all charging ports and their current availability status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ChargingPortResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var ports = await _ports.GetAllPortsAsync();
        return Ok(ports);
    }
}
