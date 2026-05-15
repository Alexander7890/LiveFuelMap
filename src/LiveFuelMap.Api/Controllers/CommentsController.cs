using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiveFuelMap.Api.Controllers;

[ApiController]
[Route("api/comments")]
public sealed class CommentsController(ICommentService commentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? stationId, CancellationToken cancellationToken)
    {
        if (stationId is <= 0)
            return BadRequest(new { error = "stationId must be greater than zero." });

        return Ok(stationId is null
            ? await commentService.ListAsync(take: 200, cancellationToken: cancellationToken)
            : await commentService.GetByStationAsync(stationId.Value, cancellationToken));
    }

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateOwn(int id, UpdateCommentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var comment = await commentService.UpdateOwnAsync(GetUserId(), id, request, cancellationToken);
            return comment is null ? NotFound() : Ok(comment);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteOwn(int id, CancellationToken cancellationToken)
    {
        try
        {
            var comment = await commentService.DeleteOwnAsync(GetUserId(), id, cancellationToken);
            return comment is null ? NotFound() : NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
