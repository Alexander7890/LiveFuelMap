using LiveFuelMap.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LiveFuelMap.Api.Controllers;

[ApiController]
[Route("api/fuels")]
public sealed class FuelsController(IFuelDataService fuelDataService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await fuelDataService.GetFuelsAsync(cancellationToken));
}
