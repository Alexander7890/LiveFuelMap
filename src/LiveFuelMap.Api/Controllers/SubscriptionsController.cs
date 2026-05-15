using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiveFuelMap.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
public sealed class SubscriptionsController(ISubscriptionService subscriptionService) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        return Ok(await subscriptionService.ListAsync(userId, cancellationToken));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(SubscriptionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            await subscriptionService.CreateAsync(userId, request, cancellationToken);
            return Created(string.Empty, new { message = "Subscription created." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        return await subscriptionService.DeleteAsync(userId, id, cancellationToken) ? NoContent() : NotFound();
    }
}
