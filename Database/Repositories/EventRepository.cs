using Dapper;
using Npgsql;
using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public class EventRepository(NpgsqlDataSource dataSource) : IEventRepository
{
    public async Task<IEnumerable<ProductionEvent>> GetEventsAsync(int? lineId, string? eventType, string? severity, int limit = 100)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QueryAsync<ProductionEvent>("""
            SELECT
                pe.id,
                pe.line_id,
                pl.name          AS line_name,
                pe.order_id,
                pe.machine_state_id,
                pe.event_type,
                pe.severity,
                pe.title,
                pe.description,
                pe.start_at::text AS start_at,
                pe.end_at::text   AS end_at,
                u.username        AS created_by,
                pe.created_at::text AS created_at
            FROM production_events pe
            JOIN production_lines pl ON pl.id = pe.line_id
            LEFT JOIN users u ON u.id = pe.created_by_id
            WHERE (@lineId    IS NULL OR pe.line_id    = @lineId)
              AND (@eventType IS NULL OR pe.event_type = @eventType)
              AND (@severity  IS NULL OR pe.severity   = @severity)
            ORDER BY pe.created_at DESC
            LIMIT @limit
            """,
            new { lineId, eventType, severity, limit });
    }

    public async Task<IEnumerable<UnacknowledgedStop>> GetUnacknowledgedStopsAsync()
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QueryAsync<UnacknowledgedStop>("""
            SELECT
                ms.id,
                ms.production_line_id AS line_id,
                pl.name               AS line_name,
                ms.ts::text           AS start_at,
                (EXTRACT(EPOCH FROM (
                    COALESCE(
                        (SELECT MIN(ts) FROM machine_states
                         WHERE production_line_id = ms.production_line_id AND ts > ms.ts),
                        NOW()
                    ) - ms.ts
                )) / 60)::int AS duration_minutes
            FROM machine_states ms
            JOIN production_lines pl ON pl.id = ms.production_line_id
            LEFT JOIN production_events pe ON pe.machine_state_id = ms.id
            WHERE ms.state = 'stopped'
              AND pe.id IS NULL
              AND ms.ts > NOW() - INTERVAL '24 hours'
            ORDER BY ms.ts DESC
            LIMIT 50
            """);
    }

    public async Task<ProductionEvent> CreateEventAsync(CreateEventRequest req, int? userId, string createdBy)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<ProductionEvent>("""
            WITH inserted AS (
                INSERT INTO production_events
                    (line_id, order_id, machine_state_id, event_type, severity,
                     title, description, start_at, end_at, created_by_id)
                VALUES
                    (@lineId, @orderId, @machineStateId, @eventType, @severity,
                     @title, @description,
                     COALESCE(@startAt::timestamptz, NOW()),
                     @endAt::timestamptz,
                     @userId)
                RETURNING *
            )
            SELECT
                i.id,
                i.line_id,
                pl.name          AS line_name,
                i.order_id,
                i.machine_state_id,
                i.event_type,
                i.severity,
                i.title,
                i.description,
                i.start_at::text AS start_at,
                i.end_at::text   AS end_at,
                @createdBy       AS created_by,
                i.created_at::text AS created_at
            FROM inserted i
            JOIN production_lines pl ON pl.id = i.line_id
            """,
            new
            {
                lineId        = req.LineId,
                orderId       = (object?)req.OrderId,
                machineStateId = (object?)req.MachineStateId,
                eventType     = req.EventType,
                severity      = req.Severity,
                title         = req.Title,
                description   = req.Description,
                startAt       = string.IsNullOrEmpty(req.StartAt) ? null : req.StartAt,
                endAt         = string.IsNullOrEmpty(req.EndAt)   ? null : req.EndAt,
                userId        = (object?)userId,
                createdBy
            });
    }

    public async Task<bool> CloseEventAsync(int id, string? endAt, string? description)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var rows = await conn.ExecuteAsync("""
            UPDATE production_events
            SET end_at      = COALESCE(@endAt::timestamptz, NOW()),
                description = COALESCE(@description, description)
            WHERE id = @id AND end_at IS NULL
            """,
            new
            {
                id,
                endAt       = string.IsNullOrEmpty(endAt)       ? null : endAt,
                description = string.IsNullOrEmpty(description) ? null : description
            });
        return rows > 0;
    }
}
