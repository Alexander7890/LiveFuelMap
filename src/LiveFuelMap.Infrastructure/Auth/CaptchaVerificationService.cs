using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.BLL.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LiveFuelMap.Infrastructure.Auth;

public sealed class CaptchaVerificationService(
    HttpClient httpClient,
    IOptions<CaptchaOptions> options,
    ILogger<CaptchaVerificationService> logger) : ICaptchaVerificationService
{
    private const string RecaptchaV2Provider = "RecaptchaV2";
    private const string CaptchaError = "Підтвердіть, що ви не робот.";
    private readonly CaptchaOptions _options = options.Value;

    public async Task VerifyAsync(string? captchaToken, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        if (!string.Equals(_options.Provider, RecaptchaV2Provider, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError("CAPTCHA provider {Provider} is not supported. Only reCAPTCHA v2 is enabled.", _options.Provider);
            throw new CaptchaVerificationException(CaptchaError);
        }

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            logger.LogError("CAPTCHA is enabled, but secret key is missing.");
            throw new CaptchaVerificationException(CaptchaError);
        }

        if (string.IsNullOrWhiteSpace(captchaToken))
        {
            logger.LogWarning("CAPTCHA token is missing. Provider={Provider}", _options.Provider);
            throw new CaptchaVerificationException(CaptchaError);
        }

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["secret"] = _options.SecretKey.Trim(),
            ["response"] = captchaToken.Trim()
        });

        using var response = await httpClient.PostAsync(_options.VerifyEndpoint, form, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("CAPTCHA provider returned HTTP {StatusCode}.", (int)response.StatusCode);
            throw new CaptchaVerificationException(CaptchaError);
        }

        var result = await response.Content.ReadFromJsonAsync<RecaptchaVerifyResponse>(cancellationToken);
        LogVerificationResult(result);

        if (result?.Success != true)
        {
            var errorCodes = result?.ErrorCodes ?? Array.Empty<string>();
            logger.LogWarning("reCAPTCHA v2 verification failed. Errors={Errors}", string.Join(",", errorCodes));
            throw new CaptchaVerificationException(CaptchaError);
        }
    }

    private void LogVerificationResult(RecaptchaVerifyResponse? result)
    {
        if (result is null)
        {
            logger.LogWarning("reCAPTCHA v2 verification returned an empty response. Provider={Provider}", _options.Provider);
            return;
        }

        logger.LogInformation(
            "reCAPTCHA v2 verification result. Provider={Provider}; Success={Success}; ChallengeTs={ChallengeTimestamp}; Errors={Errors}",
            _options.Provider,
            result.Success,
            result.ChallengeTimestamp,
            string.Join(",", result.ErrorCodes ?? Array.Empty<string>()));
    }

    private sealed record RecaptchaVerifyResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("challenge_ts")] DateTimeOffset? ChallengeTimestamp,
        [property: JsonPropertyName("error-codes")] string[]? ErrorCodes);
}
