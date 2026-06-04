using System.Collections.Concurrent;
using LiveFuelMap.BLL.DTOs;
using Microsoft.Extensions.Options;

namespace LiveFuelMap.Api.Security;

public interface IAuthAttemptRateLimiter
{
    bool TryConsume(HttpContext context, string scope, string? email, out TimeSpan retryAfter);
    void Reset(HttpContext context, string scope, string? email);
}

public sealed class InMemoryAuthAttemptRateLimiter(IOptions<AuthRateLimitOptions> options) : IAuthAttemptRateLimiter
{
    private readonly ConcurrentDictionary<string, AttemptWindow> _attempts = new(StringComparer.OrdinalIgnoreCase);
    private long _cleanupCounter;

    public bool TryConsume(HttpContext context, string scope, string? email, out TimeSpan retryAfter)
    {
        var currentOptions = options.Value;
        retryAfter = TimeSpan.Zero;
        if (!currentOptions.Enabled)
            return true;

        CleanupOccasionally();

        var limit = ResolveLimit(scope, currentOptions);
        var window = TimeSpan.FromSeconds(Math.Max(1, currentOptions.WindowSeconds));
        var now = DateTimeOffset.UtcNow;
        var keys = BuildKeys(context, scope, email);

        foreach (var key in keys)
        {
            var entry = GetCurrentWindow(key, now, window);
            lock (entry)
            {
                RefreshWindowIfExpired(entry, now, window);
                if (entry.Count >= limit)
                {
                    retryAfter = Max(retryAfter, entry.ExpiresAt - now);
                    return false;
                }
            }
        }

        foreach (var key in keys)
        {
            var entry = GetCurrentWindow(key, now, window);
            lock (entry)
            {
                RefreshWindowIfExpired(entry, now, window);
                entry.Count++;
            }
        }

        return true;
    }

    public void Reset(HttpContext context, string scope, string? email)
    {
        foreach (var key in BuildKeys(context, scope, email))
            _attempts.TryRemove(key, out _);
    }

    private AttemptWindow GetCurrentWindow(string key, DateTimeOffset now, TimeSpan window)
    {
        return _attempts.GetOrAdd(key, _ => new AttemptWindow
        {
            Count = 0,
            ExpiresAt = now.Add(window)
        });
    }

    private static void RefreshWindowIfExpired(AttemptWindow entry, DateTimeOffset now, TimeSpan window)
    {
        if (entry.ExpiresAt > now)
            return;

        entry.Count = 0;
        entry.ExpiresAt = now.Add(window);
    }

    private static int ResolveLimit(string scope, AuthRateLimitOptions options)
    {
        var configuredLimit = string.Equals(scope, "register", StringComparison.OrdinalIgnoreCase)
            ? options.RegisterAttemptLimit
            : options.LoginAttemptLimit;

        return Math.Max(1, configuredLimit);
    }

    private static IReadOnlyList<string> BuildKeys(HttpContext context, string scope, string? email)
    {
        var keys = new List<string> { $"{scope}:ip:{GetClientIp(context)}" };
        var normalizedEmail = NormalizeEmail(email);
        if (!string.IsNullOrWhiteSpace(normalizedEmail))
            keys.Add($"{scope}:email:{normalizedEmail}");

        return keys;
    }

    private static string NormalizeEmail(string? email) => email?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string GetClientIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var first = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
                return first;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;

    private void CleanupOccasionally()
    {
        if (Interlocked.Increment(ref _cleanupCounter) % 100 != 0)
            return;

        var now = DateTimeOffset.UtcNow;
        foreach (var (key, entry) in _attempts)
        {
            if (entry.ExpiresAt <= now)
                _attempts.TryRemove(key, out _);
        }
    }

    private sealed class AttemptWindow
    {
        public int Count { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
