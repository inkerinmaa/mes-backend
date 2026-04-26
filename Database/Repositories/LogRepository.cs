using Dapper;
using Npgsql;
using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public class LogRepository(NpgsqlDataSource dataSource) : ILogRepository
{
    public async Task WriteAsync(string type, string level, string message)
    {
        try
        {
            await using var conn = await dataSource.OpenConnectionAsync();
            await conn.ExecuteAsync(
                "INSERT INTO logs (type, level, message) VALUES (@type, @level, @message)",
                new { type, level, message });
        }
        catch { /* Swallow — logging must never crash the app */ }
    }

    public async Task<IEnumerable<LogEntry>> GetRecentAsync(int limit = 20, string? type = null, string? level = null)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QueryAsync<LogEntry>("""
            SELECT id, type, message, level, ts::text AS ts
            FROM logs
            WHERE (@type::text IS NULL OR type = @type)
              AND (@level::text IS NULL OR level = @level)
            ORDER BY ts DESC
            LIMIT @limit
            """,
            new { type, level, limit });
    }

    public async Task<IEnumerable<LogEntry>> GetAlertLogsAsync(IEnumerable<string> enabledTypes, DateTime? since, int limit = 30)
    {
        var types = enabledTypes.ToArray();
        if (types.Length == 0) return [];
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QueryAsync<LogEntry>("""
            SELECT id, type, message, level, ts::text AS ts
            FROM logs
            WHERE type = ANY(@types)
              AND (@since::timestamptz IS NULL OR ts > @since)
            ORDER BY ts DESC
            LIMIT @limit
            """,
            new { types, since, limit });
    }
}
