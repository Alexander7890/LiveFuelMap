using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiveFuelMap.Api.Controllers;

[ApiController]
[Route("api/stations")]
public sealed class StationsController(
    IFuelDataService fuelDataService,
    IStationAdminService stationAdminService,
    IFuelUpdatesNotifier fuelUpdatesNotifier) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? city, [FromQuery] string? name, [FromQuery] string? fuelCode, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        return Ok(await fuelDataService.GetStationsAsync(new StationListQuery(city, name, fuelCode, page, pageSize), cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var station = await fuelDataService.GetStationAsync(id, cancellationToken);
        return station is null ? NotFound() : Ok(station);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(UpsertStationRequest request, CancellationToken cancellationToken)
    {
        var station = await stationAdminService.CreateAsync(request, cancellationToken);
        await fuelUpdatesNotifier.NotifyFuelDataUpdatedAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = station.Id }, station);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertStationRequest request, CancellationToken cancellationToken)
    {
        var station = await stationAdminService.UpdateAsync(id, request, cancellationToken);
        if (station is not null)
            await fuelUpdatesNotifier.NotifyFuelDataUpdatedAsync(cancellationToken);

        return station is null ? NotFound() : Ok(station);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!await stationAdminService.DeleteAsync(id, cancellationToken))
            return NotFound();

        await fuelUpdatesNotifier.NotifyFuelDataUpdatedAsync(cancellationToken);
        return NoContent();
    }
}
