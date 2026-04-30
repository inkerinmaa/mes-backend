using Dapper;
using Npgsql;
using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public class MachineStateRepository(NpgsqlDataSource dataSource) : IMachineStateRepository
{
    public async Task<int> InsertStateAsync(int lineId, string state)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.ExecuteScalarAsync<int>(
            "INSERT INTO machine_states (production_line_id, state, ts) VALUES (@lineId, @state, NOW()) RETURNING id",
            new { lineId, state });
    }

    public async Task<IEnumerable<MachineState>> GetStatesForLineAsync(int lineId, DateTimeOffset from)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QueryAsync<MachineState>("""
            SELECT
                GREATEST(ts, @from)::text AS timestamp,
                state,
                (EXTRACT(EPOCH FROM
                    (COALESCE(LEAD(ts) OVER (ORDER BY ts), NOW())
                     - GREATEST(ts, @from))
                ) / 60)::int AS duration_minutes
            FROM machine_states
            WHERE production_line_id = @lineId
              AND ts >= COALESCE(
                  (SELECT MAX(ts) FROM machine_states
                   WHERE production_line_id = @lineId
                     AND ts < @from),
                  @from
              )
            ORDER BY ts ASC
            """,
            new { lineId, from });
    }
}
