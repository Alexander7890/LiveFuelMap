using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiveFuelMap.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/api-tokens")]
public sealed class ApiTokensController(IApiTokenService apiTokenService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ApiTokenQuery query, CancellationToken cancellationToken) =>
        Ok(await apiTokenService.ListAsync(query, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(ApiTokenCreateRequest request, CancellationToken cancellationToken)
    {
        var adminUserId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        try
        {
            return Ok(await apiTokenService.CreateAsync(adminUserId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Revoke(int id, CancellationToken cancellationToken) =>
        await apiTokenService.RevokeAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpDelete("{id:int}/permanent")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        await apiTokenService.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
}
