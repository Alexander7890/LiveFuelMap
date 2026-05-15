using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LiveFuelMap.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController(IFuelPriceReportService reportService) : ControllerBase
{
    [HttpPost("fuel-prices/export")]
    public async Task<IActionResult> ExportFuelPrices(FuelPriceExportRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var file = await reportService.ExportAsync(request, cancellationToken);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
