using System.Collections.Concurrent;
using System.Diagnostics;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;

namespace LiveFuelMap.Api.Middleware;

public sealed class ApiMetricsMiddleware(RequestDelegate next, IApiMetrics metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            var controller = context.Request.RouteValues["controller"]?.ToString() ?? "Unknown";
            metrics.Track(controller, sw.ElapsedMilliseconds);
        }
    }
}

public sealed class InMemoryApiMetrics : IApiMetrics
{
    private readonly ConcurrentDictionary<string, MetricBucket> _buckets = new(StringComparer.OrdinalIgnoreCase);

    public void Track(string controller, long elapsedMilliseconds)
    {
        var bucket = _buckets.GetOrAdd(controller, _ => new MetricBucket());
        bucket.Add(elapsedMilliseconds);
    }

    public IReadOnlyList<ApiUsageMetricDto> Snapshot() =>
        _buckets
            .OrderBy(x => x.Key)
            .Select(x => new ApiUsageMetricDto(x.Key, x.Value.Count, x.Value.AverageMilliseconds))
            .ToList();

    private sealed class MetricBucket
    {
        private long _count;
        private long _totalMilliseconds;

        public long Count => Interlocked.Read(ref _count);
        public double AverageMilliseconds => Count == 0 ? 0 : (double)Interlocked.Read(ref _totalMilliseconds) / Count;

        public void Add(long elapsedMilliseconds)
        {
            Interlocked.Increment(ref _count);
            Interlocked.Add(ref _totalMilliseconds, elapsedMilliseconds);
        }
    }
}
