using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiveFuelMap.Api.Controllers;

[ApiController]
[Route("api/compare")]
public sealed class CompareController(IFuelDataService fuelDataService) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Compare(CompareRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await fuelDataService.CompareStationsAsync(request.StationIds, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
