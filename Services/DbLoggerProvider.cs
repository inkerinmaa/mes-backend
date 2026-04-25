using Dapper;
using Npgsql;

namespace MyDashboardApi.Services;

public sealed class DbLoggerProvider(NpgsqlDataSource dataSource) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new DbLogger(dataSource, categoryName);
    public void Dispose() { }
}

public sealed class DbLogger(NpgsqlDataSource dataSource, string categoryName) : ILogger
{
    private static readonly string[] _ignoredPrefixes =
        ["Microsoft.", "System.", "Npgsql.", "Grpc.", "OpenTelemetry."];

    public bool IsEnabled(LogLevel logLevel)
        => logLevel >= LogLevel.Information
           && !_ignoredPrefixes.Any(p => categoryName.StartsWith(p, StringComparison.Ordinal));

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var message = formatter(state, exception);
        if (string.IsNullOrWhiteSpace(message)) return;

        var type = categoryName switch
        {
            _ when categoryName.Contains("UserRepository")  => "USER",
            _ when categoryName.Contains("OrderRepository") => "PROCESS",
            _ when categoryName.Contains("Hub")             => "APP",
            _                                               => "APP"
        };

        var level = logLevel switch
        {
            LogLevel.Debug       => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning     => "WARNING",
            LogLevel.Error       => "ERROR",
            LogLevel.Critical    => "CRITICAL",
            _                    => "INFO"
        };

        // Fire-and-forget — Log() is synchronous in the ILogger contract
        _ = WriteAsync(type, level, message);
    }

    private async Task WriteAsync(string type, string level, string message)
    {
        try
        {
            await using var conn = await dataSource.OpenConnectionAsync();
            await conn.ExecuteAsync(
                "INSERT INTO logs (type, level, message) VALUES (@type, @level, @message)",
                new { type, level, message });
        }
        catch { /* Must not throw — would cause infinite recursion */ }
    }
}
