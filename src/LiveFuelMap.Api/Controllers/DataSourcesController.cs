using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiveFuelMap.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/data-sources")]
public sealed class DataSourcesController(
    IDataSourceService dataSourceService,
    IFuelParserService fuelParserService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await dataSourceService.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(UpsertDataSourceRequest request, CancellationToken cancellationToken) =>
        Ok(await dataSourceService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertDataSourceRequest request, CancellationToken cancellationToken)
    {
        var source = await dataSourceService.UpdateAsync(id, request, cancellationToken);
        return source is null ? NotFound() : Ok(source);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        await dataSourceService.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("parser-runs")]
    public async Task<IActionResult> ParserRuns(CancellationToken cancellationToken) =>
        Ok(await dataSourceService.GetParserRunsAsync(cancellationToken));

    [HttpPost("parser/run")]
    public async Task<IActionResult> RunParser(CancellationToken cancellationToken) =>
        Ok(await fuelParserService.RunOnceAsync(cancellationToken));
}
