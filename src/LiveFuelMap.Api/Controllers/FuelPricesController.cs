using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiveFuelMap.Api.Controllers;

[ApiController]
[Route("api/fuel-prices")]
public sealed class FuelPricesController(
    IFuelDataService fuelDataService,
    IFuelUpdatesNotifier fuelUpdatesNotifier) : ControllerBase
{
    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] string? fuel = "all", [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] int? stationId = null, CancellationToken cancellationToken = default)
    {
        return Ok(await fuelDataService.GetPriceHistoryAsync(new PriceHistoryQuery(fuel, from, to, stationId), cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("corrections")]
    public async Task<IActionResult> Correct(UpsertFuelPriceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await fuelDataService.CorrectFuelPriceAsync(request, cancellationToken);
            await fuelUpdatesNotifier.NotifyFuelDataUpdatedAsync(
                result.Change is null ? [] : [result.Change],
                cancellationToken);

            return Ok(result.Price);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
