using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LiveFuelMap.Infrastructure.Auth;

public sealed class GoogleOAuthClient(
    HttpClient httpClient,
    IOptions<GoogleAuthOptions> options,
    IDataProtectionProvider dataProtectionProvider,
    ILogger<GoogleOAuthClient> logger) : IGoogleOAuthClient
{
    private const string FrontendCallbackPath = "/auth/google/callback";
    private readonly GoogleAuthOptions _options = options.Value;
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("LiveFuelMap.GoogleOAuth.State.v1");

    public string CreateAuthorizationUrl(string? returnUrl = null)
    {
        EnsureConfigured(requireSecret: false);

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["access_type"] = "offline",
            ["prompt"] = "select_account",
            ["include_granted_scopes"] = "true",
            ["state"] = ProtectState(NormalizeReturnUrl(returnUrl))
        };

        return AddQueryString(_options.AuthorizationEndpoint, query);
    }

    public string CreateFrontendCallbackUrl(AuthResultDto result, string? returnUrl = null)
    {
        var fragment = BuildQueryString(new Dictionary<string, string?>
        {
            ["status"] = "success",
            ["token"] = result.Token,
            ["refreshToken"] = result.RefreshToken,
            ["accessTokenExpiresAt"] = result.AccessTokenExpiresAt.ToString("O"),
            ["refreshTokenExpiresAt"] = result.RefreshTokenExpiresAt.ToString("O"),
            ["userId"] = result.UserId.ToString(),
            ["email"] = result.Email,
            ["role"] = result.Role,
            ["authProvider"] = result.AuthProvider,
            ["requiresNickname"] = result.RequiresNickname ? "true" : "false",
            ["suggestedNickname"] = result.SuggestedNickname,
            ["returnUrl"] = NormalizeReturnUrl(returnUrl)
        });

        return $"{NormalizeFrontendCallbackUrl(_options.FrontendCallbackUrl)}#{fragment}";
    }

    public string CreateFrontendErrorUrl(string error, string? returnUrl = null)
    {
        var fragment = BuildQueryString(new Dictionary<string, string?>
        {
            ["status"] = "error",
            ["message"] = string.IsNullOrWhiteSpace(error) ? "Google authentication failed." : error,
            ["returnUrl"] = NormalizeReturnUrl(returnUrl)
        });

        return $"{NormalizeFrontendCallbackUrl(_options.FrontendCallbackUrl)}#{fragment}";
    }

    public async Task<(GoogleAccountDto Account, string? ReturnUrl)> ExchangeCodeAsync(
        string code,
        string state,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured(requireSecret: true);

        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Google authorization code is missing.");

        var returnUrl = UnprotectState(state);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri,
            ["grant_type"] = "authorization_code"
        });

        using var response = await httpClient.PostAsync(_options.TokenEndpoint, content, cancellationToken);
        var tokenResponse = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken);
        if (!response.IsSuccessStatusCode || tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.IdToken))
        {
            var error = tokenResponse?.ErrorDescription ?? tokenResponse?.Error ?? response.ReasonPhrase ?? "Google token exchange failed.";
            logger.LogWarning("Google token exchange rejected: {Error}", error);
            throw new InvalidOperationException(error);
        }

        return (await ValidateIdTokenAsync(tokenResponse.IdToken), returnUrl);
    }

    public async Task<GoogleAccountDto> ValidateCredentialAsync(string credential, CancellationToken cancellationToken = default)
    {
        EnsureConfigured(requireSecret: false);

        if (string.IsNullOrWhiteSpace(credential))
            throw new InvalidOperationException("Google credential is missing.");

        return await ValidateIdTokenAsync(credential);
    }

    private async Task<GoogleAccountDto> ValidateIdTokenAsync(string idToken)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_options.ClientId],
                    IssuedAtClockTolerance = TimeSpan.FromMinutes(5),
                    ExpirationTimeClockTolerance = TimeSpan.FromMinutes(1)
                });
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning(ex, "Google ID token validation failed.");
            throw new InvalidOperationException("Google token is invalid.");
        }

        if (string.IsNullOrWhiteSpace(payload.Email))
            throw new InvalidOperationException("Google account does not contain an email address.");

        return new GoogleAccountDto(
            payload.Email,
            string.IsNullOrWhiteSpace(payload.Name) ? payload.Email.Split('@')[0] : payload.Name,
            payload.Picture,
            payload.Subject,
            payload.EmailVerified);
    }

    private string ProtectState(string? returnUrl)
    {
        var state = new GoogleOAuthState(returnUrl, DateTime.UtcNow);
        return _protector.Protect(JsonSerializer.Serialize(state));
    }

    private string? UnprotectState(string protectedState)
    {
        if (string.IsNullOrWhiteSpace(protectedState))
            throw new InvalidOperationException("Google OAuth state is missing.");

        GoogleOAuthState? state;
        try
        {
            state = JsonSerializer.Deserialize<GoogleOAuthState>(_protector.Unprotect(protectedState));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            logger.LogWarning(ex, "Google OAuth state validation failed.");
            throw new InvalidOperationException("Google OAuth state is invalid.");
        }

        if (state is null || DateTime.UtcNow - state.CreatedAt > TimeSpan.FromMinutes(Math.Max(1, _options.StateLifetimeMinutes)))
            throw new InvalidOperationException("Google OAuth state has expired.");

        return NormalizeReturnUrl(state.ReturnUrl);
    }

    private void EnsureConfigured(bool requireSecret)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
            throw new InvalidOperationException("Google Client ID is not configured.");

        if (requireSecret && string.IsNullOrWhiteSpace(_options.ClientSecret))
            throw new InvalidOperationException("Google Client Secret is not configured.");

        if (string.IsNullOrWhiteSpace(_options.RedirectUri))
            throw new InvalidOperationException("Google Redirect URI is not configured.");

        if (string.IsNullOrWhiteSpace(_options.FrontendCallbackUrl))
            throw new InvalidOperationException("Google frontend callback URL is not configured.");
    }

    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/";

        var value = returnUrl.Trim();
        return value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal)
            ? value
            : "/";
    }

    private static string NormalizeFrontendCallbackUrl(string value)
    {
        var normalized = value.Trim().TrimEnd('/');
        if (normalized.EndsWith(FrontendCallbackPath, StringComparison.OrdinalIgnoreCase))
            return normalized;

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) &&
            string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/')))
        {
            return $"{normalized}{FrontendCallbackPath}";
        }

        return normalized;
    }

    private static string AddQueryString(string endpoint, IReadOnlyDictionary<string, string?> query)
    {
        var separator = endpoint.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{endpoint}{separator}{BuildQueryString(query)}";
    }

    private static string BuildQueryString(IReadOnlyDictionary<string, string?> query) =>
        string.Join("&", query
            .Where(item => item.Value is not null)
            .Select(item => $"{WebUtility.UrlEncode(item.Key)}={WebUtility.UrlEncode(item.Value)}"));

    private sealed record GoogleOAuthState(string? ReturnUrl, DateTime CreatedAt);

    private sealed record GoogleTokenResponse(
        [property: JsonPropertyName("id_token")] string? IdToken,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("error_description")] string? ErrorDescription);
}
