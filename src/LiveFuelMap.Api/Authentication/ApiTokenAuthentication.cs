using System.Security.Claims;
using System.Text.Encodings.Web;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace LiveFuelMap.Api.Authentication;

public static class ApiTokenAuthenticationDefaults
{
    public const string Scheme = "ApiToken";
    public const string CommentsReadScheme = "ApiTokenCommentsRead";
    public const string FuelWriteScheme = "ApiTokenFuelWrite";
}

public sealed class ApiTokenAuthenticationOptions : AuthenticationSchemeOptions
{
    public string RequiredScope { get; set; } = "fuel:read";
}

public sealed class ApiTokenAuthenticationHandler(
    IOptionsMonitor<ApiTokenAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiTokenService apiTokenService) : AuthenticationHandler<ApiTokenAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = Request.Headers["X-API-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
                token = authHeader["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(token))
            return AuthenticateResult.NoResult();

        if (!await apiTokenService.ValidateAsync(token, Options.RequiredScope, Context.RequestAborted))
            return AuthenticateResult.Fail("Invalid API token.");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "api-token"),
            new Claim(ClaimTypes.Name, "Third-party API token"),
            new Claim("scope", Options.RequiredScope)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
