using LiveFuelMap.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiveFuelMap.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/metrics")]
public sealed class MetricsController(IApiMetrics metrics) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(metrics.Snapshot());
}
