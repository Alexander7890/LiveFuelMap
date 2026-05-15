using LiveFuelMap.Api.Authentication;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiveFuelMap.Api.Controllers;

[ApiController]
[Route("api/external")]
public sealed class ExternalController(
    IFuelDataService fuelDataService,
    ICommentService commentService,
    IFuelUpdatesNotifier fuelUpdatesNotifier) : ControllerBase
{
    [Authorize(AuthenticationSchemes = ApiTokenAuthenticationDefaults.Scheme)]
    [HttpGet("current-prices")]
    public async Task<IActionResult> CurrentPrices([FromQuery] string? city, [FromQuery] string? fuelCode, CancellationToken cancellationToken)
    {
        return Ok(await fuelDataService.GetStationsAsync(new StationListQuery(city, null, fuelCode, 1, 100), cancellationToken));
    }

    [Authorize(AuthenticationSchemes = ApiTokenAuthenticationDefaults.Scheme)]
    [HttpGet("fuels")]
    public async Task<IActionResult> Fuels(CancellationToken cancellationToken)
    {
        return Ok(await fuelDataService.GetFuelsAsync(cancellationToken));
    }

    [Authorize(AuthenticationSchemes = ApiTokenAuthenticationDefaults.Scheme)]
    [HttpGet("stations")]
    public async Task<IActionResult> Stations([FromQuery] string? city, [FromQuery] string? name, [FromQuery] string? fuelCode, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
    {
        return Ok(await fuelDataService.GetStationsAsync(new StationListQuery(city, name, fuelCode, page, pageSize), cancellationToken));
    }

    [Authorize(AuthenticationSchemes = ApiTokenAuthenticationDefaults.Scheme)]
    [HttpGet("price-history")]
    public async Task<IActionResult> PriceHistory([FromQuery] string? fuel = "all", [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] int? stationId = null, CancellationToken cancellationToken = default)
    {
        return Ok(await fuelDataService.GetPriceHistoryAsync(new PriceHistoryQuery(fuel, from, to, stationId), cancellationToken));
    }

    [Authorize(AuthenticationSchemes = ApiTokenAuthenticationDefaults.CommentsReadScheme)]
    [HttpGet("comments")]
    public async Task<IActionResult> Comments([FromQuery] int? stationId, [FromQuery] string? search, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        return Ok(await commentService.ListAsync(stationId, search, take, cancellationToken));
    }

    [Authorize(AuthenticationSchemes = ApiTokenAuthenticationDefaults.FuelWriteScheme)]
    [HttpGet("write-check")]
    public IActionResult WriteCheck()
    {
        return Ok(new { message = "API token has fuel:write access." });
    }

    [Authorize(AuthenticationSchemes = ApiTokenAuthenticationDefaults.FuelWriteScheme)]
    [HttpPost("fuel-prices/corrections")]
    public async Task<IActionResult> CorrectFuelPrice(UpsertFuelPriceRequest request, CancellationToken cancellationToken)
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
