using Dapper;
using Npgsql;
using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public class MachineStateRepository(NpgsqlDataSource dataSource) : IMachineStateRepository
{
    public async Task InsertStateAsync(int lineId, string state)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "INSERT INTO machine_states (production_line_id, state, ts) VALUES (@lineId, @state, NOW())",
            new { lineId, state });
    }

    public async Task<IEnumerable<MachineState>> GetStatesForLineAsync(int lineId)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QueryAsync<MachineState>("""
            SELECT
                -- Clip the first segment's start to the 8-hour boundary so the
                -- timeline never shows more than 8 hours even when the last state
                -- change happened earlier (e.g. 17 h ago and still running).
                GREATEST(ts, NOW() - INTERVAL '8 hours')::text AS timestamp,
                state,
                (EXTRACT(EPOCH FROM
                    (COALESCE(LEAD(ts) OVER (ORDER BY ts), NOW())
                     - GREATEST(ts, NOW() - INTERVAL '8 hours'))
                ) / 60)::int AS duration_minutes
            FROM machine_states
            WHERE production_line_id = @lineId
              AND ts >= COALESCE(
                  -- Include the last event that started before the 8-hour window
                  -- so the timeline has no gap at the left edge
                  (SELECT MAX(ts) FROM machine_states
                   WHERE production_line_id = @lineId
                     AND ts < NOW() - INTERVAL '8 hours'),
                  NOW() - INTERVAL '8 hours'
              )
            ORDER BY ts ASC
            """,
            new { lineId });
    }
}
