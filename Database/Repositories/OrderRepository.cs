using Dapper;
using Npgsql;
using Microsoft.Extensions.Logging;
using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public class OrderRepository(NpgsqlDataSource dataSource, ILogger<OrderRepository> logger) : IOrderRepository
{
    public async Task<IEnumerable<Order>> GetOrdersAsync()
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QueryAsync<Order>("""
            WITH active_ranked AS (
                SELECT id,
                    ROW_NUMBER() OVER (
                        PARTITION BY production_line_id
                        ORDER BY COALESCE(seq_order, 2147483647), planned_start_at NULLS LAST, created_at
                    ) AS queue_pos
                FROM orders
                WHERE status IN ('created', 'paused')
            )
            SELECT
                o.id,
                o.order_number,
                s.code               AS sku,
                o.status,
                o.priority,
                o.volume,
                u.code               AS uom_code,
                o.production_line_id AS line,
                COALESCE(o.due_date::text,      '') AS due_date,
                o.planned_start_at::text            AS planned_start_at,
                o.planned_complete_at::text         AS planned_complete_at,
                o.start_at::text                    AS start_at,
                o.complete_at::text                 AS complete_at,
                o.cage,
                COALESCE((SELECT SUM(packages) FROM cages WHERE order_number = o.order_number), 0)::int AS produced_packages,
                o.produced_volume,
                CASE WHEN u.code = 'pkg'
                     THEN COALESCE((SELECT SUM(packages) FROM cages WHERE order_number = o.order_number), 0)
                     ELSE o.pkg_produced
                END::int AS pkg_produced,
                o.comment,
                o.seq_order,
                CASE o.status
                    WHEN 'running'   THEN 'In Process'
                    WHEN 'completed' THEN 'Completed'
                    WHEN 'cancelled' THEN 'Cancelled'
                    ELSE
                        CASE ar.queue_pos
                            WHEN 1 THEN 'Next'
                            ELSE 'Next+' || (ar.queue_pos - 1)::text
                        END
                END AS sequence
            FROM orders o
            JOIN skus s ON s.id = o.sku_id
            LEFT JOIN uom u ON u.id = o.uom_id
            LEFT JOIN active_ranked ar ON ar.id = o.id
            ORDER BY
                CASE o.status
                    WHEN 'running'   THEN 0
                    WHEN 'created'   THEN 1
                    WHEN 'paused'    THEN 1
                    WHEN 'completed' THEN 2
                    WHEN 'cancelled' THEN 3
                    ELSE 4
                END,
                o.production_line_id,
                COALESCE(o.seq_order, 2147483647),
                o.planned_start_at NULLS LAST,
                o.created_at
            """);
    }

    public async Task<Order> CreateOrderAsync(CreateOrderRequest req, int? userId, string createdByUsername)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var order = await conn.QuerySingleAsync<Order>("""
            WITH inserted AS (
                INSERT INTO orders (order_number, sku_id, production_line_id, volume, uom_id, priority, due_date, planned_start_at, planned_complete_at, cage, cage_size, produced_volume, pkg_produced, created_by_id, seq_order)
                SELECT @orderNumber, s.id, @lineId, @volume, u.id, @priority,
                       @dueDate::date, @plannedStartAt::timestamptz, @plannedCompleteAt::timestamptz,
                       @cage, @cageSize,
                       CASE WHEN u.code = 'pkg' THEN 0
                            ELSE ROUND((RANDOM() * @volume)::numeric, 3)
                       END,
                       CASE WHEN u.code = 'pkg' THEN 0
                            ELSE FLOOR(RANDOM() * 500 + 1)::int
                       END,
                       @userId,
                       (SELECT COALESCE(MAX(seq_order), 0) + 1 FROM orders
                        WHERE production_line_id = @lineId AND status IN ('created', 'paused'))
                FROM skus s, uom u
                WHERE s.code = @skuCode AND u.code = @uomCode
                RETURNING id, order_number, sku_id, production_line_id, volume, uom_id,
                          status, priority, due_date, planned_start_at, planned_complete_at, cage, cage_size, produced_volume, pkg_produced, comment, seq_order
            )
            SELECT
                i.id,
                i.order_number,
                s.code               AS sku,
                i.status,
                i.priority,
                i.volume,
                u.code               AS uom_code,
                i.production_line_id AS line,
                COALESCE(i.due_date::text,          '') AS due_date,
                i.planned_start_at::text                AS planned_start_at,
                i.planned_complete_at::text             AS planned_complete_at,
                NULL::text                              AS start_at,
                NULL::text                              AS complete_at,
                'Next'               AS sequence,
                i.cage,
                i.cage_size,
                0                    AS produced_packages,
                i.produced_volume,
                i.pkg_produced,
                i.comment
            FROM inserted i
            JOIN skus s ON s.id = i.sku_id
            LEFT JOIN uom u ON u.id = i.uom_id
            """,
            new
            {
                orderNumber      = req.OrderNumber,
                skuCode          = req.SkuCode,
                lineId           = req.LineId,
                volume           = req.Volume,
                uomCode          = req.UomCode,
                priority         = req.Priority,
                dueDate          = string.IsNullOrEmpty(req.DueDate)          ? null : req.DueDate,
                plannedStartAt   = string.IsNullOrEmpty(req.PlannedStartAt)   ? null : req.PlannedStartAt,
                plannedCompleteAt = string.IsNullOrEmpty(req.PlannedCompleteAt) ? null : req.PlannedCompleteAt,
                cage             = req.Cage,
                cageSize         = req.Cage ? req.CageSize : 50,
                userId           = (object?)userId
            });

        logger.LogInformation(
            "Order created: #{OrderNumber} | SKU={Sku} | Line={Line} | Volume={Volume} {Uom} | Priority={Priority} | Cage={Cage} | By={User}",
            order.OrderNumber, order.Sku, order.Line, order.Volume, order.UomCode, order.Priority, order.Cage, createdByUsername);

        return order;
    }

    public async Task<bool> CancelOrderAsync(int orderId, string cancelledBy)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var orderNumber = await conn.ExecuteScalarAsync<string?>("""
            UPDATE orders SET status = 'cancelled', updated_at = NOW()
            WHERE id = @orderId AND status NOT IN ('completed', 'cancelled')
            RETURNING order_number
            """,
            new { orderId });

        if (orderNumber != null)
            logger.LogInformation("Order cancelled: #{OrderNumber} (id={Id}) by {User}", orderNumber, orderId, cancelledBy);

        return orderNumber != null;
    }

    public async Task<(bool Success, string? Error)> TransitionStatusAsync(int orderId, string action, string username)
    {
        await using var conn = await dataSource.OpenConnectionAsync();

        string[] fromStates;
        string toStatus;
        string extraSet;

        switch (action)
        {
            case "start":
                fromStates = ["created", "paused"];
                toStatus   = "running";
                extraSet   = ", start_at = COALESCE(start_at, NOW())";
                break;
            case "pause":
                fromStates = ["running"];
                toStatus   = "paused";
                extraSet   = "";
                break;
            case "complete":
                fromStates = ["running"];
                toStatus   = "completed";
                extraSet   = ", complete_at = NOW()";
                break;
            default:
                return (false, $"Unknown action '{action}'. Use 'start', 'pause', or 'complete'.");
        }

        var orderNumber = await conn.ExecuteScalarAsync<string?>(
            $"""
            UPDATE orders SET status = @toStatus, updated_at = NOW(){extraSet}
            WHERE id = @orderId AND status = ANY(@fromStates)
            RETURNING order_number
            """,
            new { orderId, toStatus, fromStates });

        if (orderNumber == null)
        {
            var current = await conn.ExecuteScalarAsync<string?>(
                "SELECT status FROM orders WHERE id = @orderId", new { orderId });
            if (current == null) return (false, "Order not found");
            return (false, $"Cannot '{action}' an order in '{current}' state");
        }

        logger.LogInformation("Order {Action}: #{OrderNumber} (id={Id}) by {User}",
            action, orderNumber, orderId, username);
        return (true, null);
    }

    public async Task<OrderDetail?> GetOrderDetailAsync(int orderId)
    {
        await using var conn = await dataSource.OpenConnectionAsync();

        var detail = await conn.QuerySingleOrDefaultAsync<OrderDetail>("""
            SELECT
                o.id,
                o.order_number,
                s.code          AS sku,
                o.priority,
                o.volume,
                u.code          AS uom_code,
                u.name          AS uom_name,
                o.production_line_id AS line,
                COALESCE(o.due_date::text, '') AS due_date,
                o.status,
                o.planned_start_at::text   AS planned_start_at,
                o.planned_complete_at::text AS planned_complete_at,
                o.start_at::text           AS start_at,
                o.complete_at::text        AS complete_at,
                o.cage,
                o.cage_size,
                o.comment,
                COALESCE((SELECT SUM(packages) FROM cages WHERE order_number = o.order_number), 0)::int AS produced_packages,
                o.produced_volume,
                CASE WHEN u.code = 'pkg'
                     THEN COALESCE((SELECT SUM(packages) FROM cages WHERE order_number = o.order_number), 0)
                     ELSE o.pkg_produced
                END::int AS pkg_produced
            FROM orders o
            JOIN skus s ON s.id = o.sku_id
            LEFT JOIN uom u ON u.id = o.uom_id
            WHERE o.id = @orderId
            """,
            new { orderId });

        if (detail == null) return null;

        if (detail.Cage)
        {
            detail.Cages = (await conn.QueryAsync<CageEntry>("""
                SELECT
                    c.id,
                    c.cage_guid::text AS cage_guid,
                    c.cage_size,
                    c.packages,
                    c.completed_at::text AS completed_at,
                    u.username AS completed_by
                FROM cages c
                LEFT JOIN users u ON u.id = c.completed_by_id
                WHERE c.order_number = @orderNumber
                ORDER BY c.completed_at DESC
                """,
                new { orderNumber = detail.OrderNumber })).ToList();
        }

        return detail;
    }

    public async Task<CageEntry?> AddCageAsync(string orderNumber, int cageSize, int? userId)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<CageEntry>("""
            INSERT INTO cages (order_number, cage_size, packages, completed_by_id)
            VALUES (@orderNumber, @cageSize, @cageSize, @userId)
            RETURNING id, cage_guid::text AS cage_guid, cage_size, packages, completed_at::text AS completed_at, NULL AS completed_by
            """,
            new { orderNumber, cageSize, userId = (object?)userId });
    }

    public async Task<bool> UpdateCagePackagesAsync(int cageId, int packages)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var rows = await conn.ExecuteAsync(
            "UPDATE cages SET packages = @packages WHERE id = @cageId",
            new { cageId, packages });
        return rows > 0;
    }

    public async Task<bool> UpdateOrderCommentAsync(int orderId, string? comment)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var rows = await conn.ExecuteAsync(
            "UPDATE orders SET comment = @comment, updated_at = NOW() WHERE id = @orderId",
            new { orderId, comment });
        return rows > 0;
    }

    public async Task<bool> DeleteCageAsync(int cageId)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var rows = await conn.ExecuteAsync(
            "DELETE FROM cages WHERE id = @cageId",
            new { cageId });
        return rows > 0;
    }

    public async Task<bool> ResequenceOrderAsync(int orderId, string direction)
    {
        await using var conn = await dataSource.OpenConnectionAsync();

        var order = await conn.QuerySingleOrDefaultAsync<SeqRow>(
            "SELECT id, seq_order, production_line_id FROM orders WHERE id = @orderId AND status IN ('created', 'paused')",
            new { orderId });

        if (order is null) return false;

        var adjacent = direction == "up"
            ? await conn.QuerySingleOrDefaultAsync<SeqRow>("""
                SELECT id, seq_order, production_line_id FROM orders
                WHERE production_line_id = @lineId
                  AND status IN ('created', 'paused')
                  AND id != @orderId
                  AND COALESCE(seq_order, 2147483647) < COALESCE(@seqOrder, 2147483647)
                ORDER BY COALESCE(seq_order, 2147483647) DESC
                LIMIT 1
                """,
                new { lineId = order.ProductionLineId, orderId, seqOrder = order.SeqOrder })
            : await conn.QuerySingleOrDefaultAsync<SeqRow>("""
                SELECT id, seq_order, production_line_id FROM orders
                WHERE production_line_id = @lineId
                  AND status IN ('created', 'paused')
                  AND id != @orderId
                  AND COALESCE(seq_order, 2147483647) > COALESCE(@seqOrder, 2147483647)
                ORDER BY COALESCE(seq_order, 2147483647) ASC
                LIMIT 1
                """,
                new { lineId = order.ProductionLineId, orderId, seqOrder = order.SeqOrder });

        if (adjacent is null) return false;

        await using var tx = await conn.BeginTransactionAsync();
        await conn.ExecuteAsync("UPDATE orders SET seq_order = @seq WHERE id = @id",
            new { seq = adjacent.SeqOrder, id = orderId }, tx);
        await conn.ExecuteAsync("UPDATE orders SET seq_order = @seq WHERE id = @id",
            new { seq = order.SeqOrder, id = adjacent.Id }, tx);
        await tx.CommitAsync();
        return true;
    }

    public async Task<bool> RescheduleOrderAsync(int orderId, string? plannedStartAt, string? plannedCompleteAt)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var rows = await conn.ExecuteAsync("""
            UPDATE orders
            SET planned_start_at    = @plannedStartAt::timestamptz,
                planned_complete_at = @plannedCompleteAt::timestamptz,
                updated_at          = NOW()
            WHERE id = @orderId AND status NOT IN ('completed', 'cancelled')
            """,
            new
            {
                orderId,
                plannedStartAt    = string.IsNullOrEmpty(plannedStartAt)    ? null : plannedStartAt,
                plannedCompleteAt = string.IsNullOrEmpty(plannedCompleteAt) ? null : plannedCompleteAt
            });
        return rows > 0;
    }

    private class SeqRow
    {
        public int Id { get; set; }
        public int? SeqOrder { get; set; }
        public int ProductionLineId { get; set; }
    }
}
