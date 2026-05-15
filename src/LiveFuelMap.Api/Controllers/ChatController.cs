using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.BLL.Services;
using Microsoft.AspNetCore.Mvc;

namespace LiveFuelMap.Api.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController(IChatService chatService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Ask(ChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return Ok(await chatService.AskAsync(request, userId, ip, cancellationToken));
        }
        catch (ChatRateLimitExceededException)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "Too many chat messages. Try again later." });
        }
        catch (AiChatUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { answer = "AI сервіс тимчасово недоступний.", status = "failed" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return BadRequest(new { error = "sessionId is required." });

        return Ok(await chatService.GetHistoryAsync(sessionId, GetUserId(), cancellationToken));
    }

    [HttpDelete("history")]
    public async Task<IActionResult> ClearHistory([FromQuery] string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return BadRequest(new { error = "sessionId is required." });

        await chatService.ClearHistoryAsync(sessionId, GetUserId(), cancellationToken);
        return NoContent();
    }

    private int? GetUserId()
    {
        if (User.Identity?.IsAuthenticated != true)
            return null;

        var value = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                    User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, out var userId) ? userId : null;
    }
}
