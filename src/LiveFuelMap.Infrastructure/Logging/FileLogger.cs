using Microsoft.Extensions.Logging;

namespace LiveFuelMap.Infrastructure.Logging;

public sealed class FileLoggerProvider(string path) : ILoggerProvider
{
    private static readonly object WriteLock = new();

    public ILogger CreateLogger(string categoryName) => new FileLogger(path, categoryName, WriteLock);

    public void Dispose()
    {
    }
}

public sealed class FileLogger(string path, string category, object writeLock) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var line = $"{DateTimeOffset.UtcNow:O} [{logLevel}] {category}: {formatter(state, exception)}";
        if (exception is not null)
            line += Environment.NewLine + exception;

        lock (writeLock)
        {
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream);
            writer.WriteLine(line);
        }
    }
}
