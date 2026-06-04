using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LiveFuelMap.Api.Security;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiveFuelMap.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    IGoogleOAuthClient googleOAuthClient,
    IAuthAttemptRateLimiter authAttemptRateLimiter,
    ILogger<AuthController> logger) : ControllerBase
{
    private const string TooManyLoginAttemptsMessage = "Забагато спроб входу. Спробуйте ще раз через кілька хвилин.";
    private const string TooManyRegisterAttemptsMessage = "Забагато спроб реєстрації. Спробуйте ще раз через кілька хвилин.";

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var rateLimitResponse = CreateRateLimitResponse("register", request.Email, TooManyRegisterAttemptsMessage);
        if (rateLimitResponse is not null)
            return rateLimitResponse;

        try
        {
            await authService.RegisterAsync(request, cancellationToken);
            authAttemptRateLimiter.Reset(HttpContext, "register", request.Email);
            return Created(string.Empty, new { message = "Акаунт створено." });
        }
        catch (NicknameUnavailableException ex)
        {
            return BadRequest(new
            {
                message = ex.Message,
                error = ex.Message,
                errors = new Dictionary<string, string[]> { ["nickname"] = [ex.Message] },
                suggestions = ex.Suggestions
            });
        }
        catch (ValidationFailedException ex)
        {
            return BadRequest(ToErrorResponse(ex.Message, ex.Errors));
        }
        catch (CaptchaVerificationException ex)
        {
            return BadRequest(ToErrorResponse(ex.Message, new Dictionary<string, string[]> { ["captchaToken"] = [ex.Message] }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ToErrorResponse(ex.Message));
        }
    }

    [HttpGet("nickname-suggestions")]
    public async Task<IActionResult> NicknameSuggestions([FromQuery] string nickname, CancellationToken cancellationToken)
    {
        return Ok(new { suggestions = await authService.SuggestNicknamesAsync(nickname, 3, cancellationToken) });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var rateLimitResponse = CreateRateLimitResponse("login", request.Email, TooManyLoginAttemptsMessage);
        if (rateLimitResponse is not null)
            return rateLimitResponse;

        try
        {
            var result = await authService.LoginAsync(request, cancellationToken);
            authAttemptRateLimiter.Reset(HttpContext, "login", request.Email);
            return Ok(result);
        }
        catch (CaptchaVerificationException ex)
        {
            return BadRequest(ToErrorResponse(ex.Message, new Dictionary<string, string[]> { ["captchaToken"] = [ex.Message] }));
        }
        catch (ValidationFailedException ex)
        {
            return BadRequest(ToErrorResponse(ex.Message, ex.Errors));
        }
        catch (AuthenticationFailedException ex)
        {
            return Unauthorized(ToErrorResponse(ex.Message, ex.Errors));
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(ToErrorResponse(ex.Message));
        }
    }

    [HttpGet("google/start")]
    public IActionResult GoogleStart([FromQuery] string? returnUrl = null)
    {
        try
        {
            return Redirect(googleOAuthClient.CreateAuthorizationUrl(returnUrl));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error))
            return Redirect(googleOAuthClient.CreateFrontendErrorUrl($"Google authentication was cancelled or failed: {error}"));

        try
        {
            var (account, returnUrl) = await googleOAuthClient.ExchangeCodeAsync(code ?? string.Empty, state ?? string.Empty, cancellationToken);
            var result = await authService.LoginWithGoogleAsync(account, cancellationToken);
            return Redirect(googleOAuthClient.CreateFrontendCallbackUrl(result, returnUrl));
        }
        catch (InvalidOperationException ex)
        {
            return Redirect(googleOAuthClient.CreateFrontendErrorUrl(ex.Message));
        }
    }

    [HttpPost("google/credential")]
    public async Task<IActionResult> GoogleCredential(GoogleCredentialLoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var account = await googleOAuthClient.ValidateCredentialAsync(request.Credential, cancellationToken);
            return Ok(await authService.LoginWithGoogleAsync(account, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await authService.RefreshAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request, CancellationToken cancellationToken)
    {
        var userId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        await authService.LogoutAsync(userId, request ?? new LogoutRequest(null), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            await authService.ChangePasswordAsync(userId, request, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            await authService.VerifyEmailAsync(userId, request, cancellationToken);
            return Ok(new { message = "Email verified." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("resend-verification-code")]
    public async Task<IActionResult> ResendVerificationCode(CancellationToken cancellationToken)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            await authService.ResendEmailVerificationCodeAsync(userId, cancellationToken);
            return Ok(new { message = "Verification code sent." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("verify")]
    public async Task<IActionResult> Verify(CancellationToken cancellationToken)
    {
        var userId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        return Ok(await authService.GetCurrentUserAsync(userId, cancellationToken));
    }

    private static object ToErrorResponse(string message, IReadOnlyDictionary<string, string[]>? errors = null) =>
        new
        {
            message,
            error = message,
            errors = errors ?? new Dictionary<string, string[]>()
        };

    private IActionResult? CreateRateLimitResponse(string scope, string? email, string message)
    {
        if (authAttemptRateLimiter.TryConsume(HttpContext, scope, email, out var retryAfter))
            return null;

        Response.Headers["Retry-After"] = Math.Ceiling(retryAfter.TotalSeconds).ToString("0");
        logger.LogWarning(
            "Authentication rate limit exceeded. Scope={Scope}; Email={Email}; RetryAfterSeconds={RetryAfterSeconds}",
            scope,
            NormalizeEmailForLog(email),
            Math.Ceiling(retryAfter.TotalSeconds));

        return StatusCode(StatusCodes.Status429TooManyRequests, ToErrorResponse(message));
    }

    private static string NormalizeEmailForLog(string? email) => email?.Trim().ToLowerInvariant() ?? "unknown";
}
