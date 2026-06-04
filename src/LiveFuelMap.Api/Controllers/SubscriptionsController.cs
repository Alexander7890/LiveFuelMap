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
            var subscriptions = await subscriptionService.CreateAsync(userId, request, cancellationToken);
            return Created(string.Empty, new
            {
                message = "Підписку збережено. Ви отримуватимете оновлення згідно з обраним графіком.",
                subscriptions
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SubscriptionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            var subscription = await subscriptionService.UpdateAsync(userId, id, request, cancellationToken);
            return subscription is null
                ? NotFound()
                : Ok(new
                {
                    message = "Підписку збережено. Ви отримуватимете оновлення згідно з обраним графіком.",
                    subscription
                });
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
