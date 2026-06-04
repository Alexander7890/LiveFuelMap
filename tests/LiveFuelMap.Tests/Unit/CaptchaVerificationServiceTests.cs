using System.Net;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Services;
using LiveFuelMap.Infrastructure.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LiveFuelMap.Tests.Unit;

public sealed class CaptchaVerificationServiceTests
{
    private const string UserMessage = "Підтвердіть, що ви не робот.";

    [Fact]
    public async Task VerifyAsync_WhenDisabled_AllowsMissingToken()
    {
        var service = CreateService(new CaptchaOptions { Enabled = false });

        await service.VerifyAsync(null);
    }

    [Fact]
    public async Task VerifyAsync_WhenEnabledAndTokenMissing_ThrowsUserMessage()
    {
        var service = CreateService(new CaptchaOptions
        {
            Enabled = true,
            Provider = "RecaptchaV2",
            SecretKey = "secret"
        });

        var ex = await Assert.ThrowsAsync<CaptchaVerificationException>(() => service.VerifyAsync(""));
        Assert.Equal(UserMessage, ex.Message);
    }

    [Fact]
    public async Task VerifyAsync_WhenProviderAcceptsToken_SendsSecretAndResponse()
    {
        var handler = new CapturingHandler("{\"success\":true}");
        var service = CreateService(new CaptchaOptions
        {
            Enabled = true,
            Provider = "RecaptchaV2",
            SecretKey = "secret-key",
            VerifyEndpoint = "https://captcha.test/siteverify"
        }, handler);

        await service.VerifyAsync("captcha-token");

        Assert.Equal("https://captcha.test/siteverify", handler.RequestUri?.ToString());
        Assert.Contains("secret=secret-key", handler.Body);
        Assert.Contains("response=captcha-token", handler.Body);
    }

    [Fact]
    public async Task VerifyAsync_WhenProviderRejectsToken_ThrowsUserMessage()
    {
        var service = CreateService(new CaptchaOptions
        {
            Enabled = true,
            Provider = "RecaptchaV2",
            SecretKey = "secret"
        }, new CapturingHandler("{\"success\":false,\"error-codes\":[\"invalid-input-response\"]}"));

        var ex = await Assert.ThrowsAsync<CaptchaVerificationException>(() => service.VerifyAsync("bad-token"));
        Assert.Equal(UserMessage, ex.Message);
    }

    [Fact]
    public async Task VerifyAsync_WhenProviderIsNotV2_ThrowsUserMessage()
    {
        var service = CreateService(new CaptchaOptions
        {
            Enabled = true,
            Provider = "Turnstile",
            SecretKey = "secret"
        }, new CapturingHandler("{\"success\":true}"));

        var ex = await Assert.ThrowsAsync<CaptchaVerificationException>(() => service.VerifyAsync("captcha-token"));
        Assert.Equal(UserMessage, ex.Message);
    }

    private static CaptchaVerificationService CreateService(CaptchaOptions options, HttpMessageHandler? handler = null)
    {
        return new CaptchaVerificationService(
            new HttpClient(handler ?? new CapturingHandler("{\"success\":true}")),
            Options.Create(options),
            NullLogger<CaptchaVerificationService>.Instance);
    }

    private sealed class CapturingHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            };
        }
    }
}
