using Dapper;
using Npgsql;
using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public class SettingsRepository(NpgsqlDataSource dataSource) : ISettingsRepository
{
    public async Task<IEnumerable<Setting>> GetAllAsync()
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QueryAsync<Setting>("""
            SELECT key, value, previous_value, changed_by_id, changed_at::text
            FROM settings
            ORDER BY key
            """);
    }

    public async Task<Setting?> SetAsync(string key, string value, int? changedById)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<Setting>("""
            UPDATE settings
            SET value          = @value,
                previous_value = value,
                changed_by_id  = @changedById,
                changed_at     = NOW()
            WHERE key = @key
            RETURNING key, value, previous_value, changed_by_id, changed_at::text
            """,
            new { key, value, changedById });
    }
}
