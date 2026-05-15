using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiveFuelMap.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/comments")]
public sealed class AdminCommentsController(ICommentService commentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? stationId, [FromQuery] string? search, [FromQuery] int take = 200, CancellationToken cancellationToken = default)
    {
        return Ok(await commentService.ListAsync(stationId, search, take, cancellationToken));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateCommentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var comment = await commentService.UpdateAsync(id, request, cancellationToken);
            return comment is null ? NotFound() : Ok(comment);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        return await commentService.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
    }
}
