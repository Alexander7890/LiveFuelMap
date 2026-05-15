using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using LiveFuelMap.BLL.DTOs;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace LiveFuelMap.Api.Middleware;

public static class ApiSecurityServiceCollectionExtensions
{
    public static IServiceCollection AddLiveFuelMapApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApiSecurityOptions>(configuration.GetSection("ApiSecurity"));
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        services.AddRateLimiter(options =>
        {
            var rateLimitOptions = configuration.GetSection("ApiSecurity:RateLimit").Get<ApiRateLimitOptions>() ?? new ApiRateLimitOptions();
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers["Retry-After"] = Math.Ceiling(retryAfter.TotalSeconds).ToString("0");
                }

                context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
                await context.HttpContext.Response.WriteAsync(
                    """{"error":"Too many requests. Try again later."}""",
                    cancellationToken);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                if (!rateLimitOptions.Enabled || !httpContext.Request.Path.StartsWithSegments("/api"))
                {
                    return RateLimitPartition.GetNoLimiter("api-security-disabled");
                }

                var method = httpContext.Request.Method;
                if (HttpMethods.IsOptions(method))
                {
                    return RateLimitPartition.GetNoLimiter("api-security-options");
                }

                var isWrite = !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method);
                var permitLimit = isWrite ? rateLimitOptions.WritePermitLimit : rateLimitOptions.ReadPermitLimit;
                var partitionKey = $"{GetClientIp(httpContext)}:{(isWrite ? "write" : "read")}";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, permitLimit),
                    Window = TimeSpan.FromSeconds(Math.Max(1, rateLimitOptions.WindowSeconds)),
                    QueueLimit = Math.Max(0, rateLimitOptions.QueueLimit),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                });
            });
        });

        return services;
    }

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
}

public sealed class IdempotencyMiddleware(
    RequestDelegate next,
    IIdempotencyStore store,
    IOptions<ApiSecurityOptions> options,
    ILogger<IdempotencyMiddleware> logger)
{
    private static readonly HashSet<string> ProtectedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    };

    public async Task InvokeAsync(HttpContext context)
    {
        var idempotencyOptions = options.Value.Idempotency;
        if (!ShouldProcess(context, idempotencyOptions))
        {
            await next(context);
            return;
        }

        var key = context.Request.Headers[idempotencyOptions.HeaderName].ToString().Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            await next(context);
            return;
        }

        if (!IsValidKey(key, idempotencyOptions.MaxKeyLength))
        {
            await WriteJsonAsync(context, StatusCodes.Status400BadRequest, "Invalid Idempotency-Key header.");
            return;
        }

        var fingerprint = await TryBuildFingerprintAsync(context, idempotencyOptions.MaxBodyBytes, context.RequestAborted);
        if (fingerprint is null)
        {
            await WriteJsonAsync(context, StatusCodes.Status413PayloadTooLarge, "Request body is too large for idempotency protection.");
            return;
        }

        var owner = ResolveOwner(context);
        var ttl = TimeSpan.FromMinutes(Math.Max(1, idempotencyOptions.KeyTtlMinutes));
        var beginResult = store.TryBegin(owner, key, fingerprint, ttl, out var entry);

        if (beginResult == IdempotencyBeginResult.Replay && entry?.Response is not null)
        {
            context.Response.Headers["Idempotency-Replayed"] = "true";
            context.Response.StatusCode = entry.Response.StatusCode;
            context.Response.ContentType = entry.Response.ContentType;
            await context.Response.Body.WriteAsync(entry.Response.Body, context.RequestAborted);
            return;
        }

        if (beginResult == IdempotencyBeginResult.Conflict)
        {
            await WriteJsonAsync(context, StatusCodes.Status409Conflict, "This Idempotency-Key was already used for another request.");
            return;
        }

        if (beginResult == IdempotencyBeginResult.InProgress)
        {
            await WriteJsonAsync(context, StatusCodes.Status409Conflict, "This Idempotency-Key is already being processed.");
            return;
        }

        var originalBody = context.Response.Body;
        await using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await next(context);
            responseBuffer.Position = 0;
            var responseBytes = responseBuffer.ToArray();

            if (context.Response.StatusCode < StatusCodes.Status500InternalServerError)
            {
                store.Complete(owner, key, fingerprint, new IdempotencyResponse(
                    context.Response.StatusCode,
                    context.Response.ContentType,
                    responseBytes));

                context.Response.Headers["Idempotency-Stored"] = "true";
            }
            else
            {
                store.Remove(owner, key);
            }

            context.Response.Body = originalBody;
            await originalBody.WriteAsync(responseBytes, context.RequestAborted);
        }
        catch (Exception ex)
        {
            store.Remove(owner, key);
            logger.LogWarning(ex, "Idempotent request failed. Key={Key}; Owner={Owner}", key, owner);
            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static bool ShouldProcess(HttpContext context, IdempotencyOptions options)
    {
        return options.Enabled &&
            context.Request.Path.StartsWithSegments("/api") &&
            ProtectedMethods.Contains(context.Request.Method);
    }

    private static bool IsValidKey(string key, int maxLength)
    {
        if (key.Length < 8 || key.Length > Math.Max(8, maxLength))
            return false;

        foreach (var c in key)
        {
            if (!char.IsLetterOrDigit(c) && c is not '-' and not '_' and not ':' and not '.')
                return false;
        }

        return true;
    }

    private static async Task<string?> TryBuildFingerprintAsync(HttpContext context, int maxBodyBytes, CancellationToken cancellationToken)
    {
        try
        {
            context.Request.EnableBuffering(bufferThreshold: 64 * 1024, bufferLimit: Math.Max(1, maxBodyBytes));
            await using var body = new MemoryStream();
            await context.Request.Body.CopyToAsync(body, cancellationToken);
            context.Request.Body.Position = 0;

            var bodyHash = Convert.ToHexString(SHA256.HashData(body.ToArray()));
            var raw = string.Join('|',
                context.Request.Method.ToUpperInvariant(),
                context.Request.Path.Value,
                context.Request.QueryString.Value,
                context.Request.ContentType,
                bodyHash);

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string ResolveOwner(HttpContext context)
    {
        var userId = context.User.FindFirstValue("sub") ??
            context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(userId))
            return $"user:{userId}";

        var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var first = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
                return $"ip:{first}";
        }

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    private static async Task WriteJsonAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync($$"""{"error":"{{message}}"}""", context.RequestAborted);
    }
}

public interface IIdempotencyStore
{
    IdempotencyBeginResult TryBegin(
        string owner,
        string key,
        string fingerprint,
        TimeSpan ttl,
        out IdempotencyEntry? entry);

    void Complete(string owner, string key, string fingerprint, IdempotencyResponse response);
    void Remove(string owner, string key);
}

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyEntry> _entries = new(StringComparer.Ordinal);
    private long _accessCount;

    public IdempotencyBeginResult TryBegin(
        string owner,
        string key,
        string fingerprint,
        TimeSpan ttl,
        out IdempotencyEntry? entry)
    {
        CleanupOccasionally();
        var storeKey = BuildStoreKey(owner, key);
        var now = DateTimeOffset.UtcNow;

        while (true)
        {
            var candidate = new IdempotencyEntry(fingerprint, now.Add(ttl));
            var current = _entries.GetOrAdd(storeKey, candidate);
            entry = current;

            lock (current)
            {
                if (current.ExpiresAt <= now)
                {
                    _entries.TryRemove(storeKey, out _);
                    continue;
                }

                if (ReferenceEquals(current, candidate))
                    return IdempotencyBeginResult.Started;

                if (!string.Equals(current.Fingerprint, fingerprint, StringComparison.Ordinal))
                    return IdempotencyBeginResult.Conflict;

                return current.State == IdempotencyEntryState.Completed
                    ? IdempotencyBeginResult.Replay
                    : IdempotencyBeginResult.InProgress;
            }
        }
    }

    public void Complete(string owner, string key, string fingerprint, IdempotencyResponse response)
    {
        var storeKey = BuildStoreKey(owner, key);
        if (!_entries.TryGetValue(storeKey, out var entry))
            return;

        lock (entry)
        {
            if (!string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal))
                return;

            entry.Response = response;
            entry.State = IdempotencyEntryState.Completed;
        }
    }

    public void Remove(string owner, string key)
    {
        _entries.TryRemove(BuildStoreKey(owner, key), out _);
    }

    private void CleanupOccasionally()
    {
        if (Interlocked.Increment(ref _accessCount) % 100 != 0)
            return;

        var now = DateTimeOffset.UtcNow;
        foreach (var (key, entry) in _entries)
        {
            if (entry.ExpiresAt <= now)
                _entries.TryRemove(key, out _);
        }
    }

    private static string BuildStoreKey(string owner, string key) => $"{owner}:{key}";
}

public sealed class IdempotencyEntry(string fingerprint, DateTimeOffset expiresAt)
{
    public string Fingerprint { get; } = fingerprint;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
    public IdempotencyEntryState State { get; set; } = IdempotencyEntryState.InProgress;
    public IdempotencyResponse? Response { get; set; }
}

public sealed record IdempotencyResponse(int StatusCode, string? ContentType, byte[] Body);

public enum IdempotencyBeginResult
{
    Started,
    Replay,
    Conflict,
    InProgress
}

public enum IdempotencyEntryState
{
    InProgress,
    Completed
}
