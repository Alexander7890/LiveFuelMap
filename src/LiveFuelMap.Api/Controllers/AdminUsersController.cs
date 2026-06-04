using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiveFuelMap.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/users")]
public sealed class AdminUsersController(IUserAdminService userAdminService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] UserAdminQuery query, CancellationToken cancellationToken) =>
        Ok(await userAdminService.ListAsync(query, cancellationToken));

    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> UpdateRole(int id, UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        var user = await userAdminService.UpdateRoleAsync(id, request.Role, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            var currentAdminUserId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            return await userAdminService.DeleteAsync(id, currentAdminUserId, cancellationToken) ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
