using System.Net;
using LiveFuelMap.Api.Security;
using LiveFuelMap.BLL.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LiveFuelMap.Tests.Unit;

public sealed class AuthAttemptRateLimiterTests
{
    [Fact]
    public void TryConsume_WhenLoginAttemptsExceedLimit_BlocksByEmail()
    {
        var limiter = CreateLimiter(new AuthRateLimitOptions
        {
            Enabled = true,
            LoginAttemptLimit = 2,
            WindowSeconds = 300
        });
        var context = CreateContext("127.0.0.1");

        Assert.True(limiter.TryConsume(context, "login", "user@example.com", out _));
        Assert.True(limiter.TryConsume(context, "login", "USER@example.com", out _));
        Assert.False(limiter.TryConsume(CreateContext("127.0.0.2"), "login", " user@example.com ", out var retryAfter));
        Assert.True(retryAfter > TimeSpan.Zero);
    }

    [Fact]
    public void Reset_WhenLoginSucceeds_AllowsNewAttempts()
    {
        var limiter = CreateLimiter(new AuthRateLimitOptions
        {
            Enabled = true,
            LoginAttemptLimit = 1,
            WindowSeconds = 300
        });
        var context = CreateContext("127.0.0.1");

        Assert.True(limiter.TryConsume(context, "login", "user@example.com", out _));
        Assert.False(limiter.TryConsume(context, "login", "user@example.com", out _));

        limiter.Reset(context, "login", "user@example.com");

        Assert.True(limiter.TryConsume(context, "login", "user@example.com", out _));
    }

    private static InMemoryAuthAttemptRateLimiter CreateLimiter(AuthRateLimitOptions options) =>
        new(Options.Create(options));

    private static DefaultHttpContext CreateContext(string ip)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }
}
