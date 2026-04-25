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
            SELECT
                id,
                order_number,
                sku,
                priority,
                volume,
                uom_code,
                line,
                due_date,
                CASE rn
                    WHEN 1 THEN 'In Progress'
                    WHEN 2 THEN 'Next'
                    ELSE 'Next+' || (rn - 2)::text
                END AS sequence,
                cage,
                produced_packages,
                comment
            FROM (
                SELECT
                    o.id,
                    o.order_number,
                    s.code              AS sku,
                    o.priority,
                    o.volume,
                    u.code              AS uom_code,
                    o.production_line_id AS line,
                    COALESCE(o.due_date::text, '') AS due_date,
                    o.cage,
                    COALESCE((SELECT SUM(packages) FROM cages WHERE order_number = o.order_number), 0)::int AS produced_packages,
                    o.comment,
                    ROW_NUMBER() OVER (ORDER BY o.created_at) AS rn
                FROM orders o
                JOIN skus s ON s.id = o.sku_id
                LEFT JOIN uom u ON u.id = o.uom_id
                WHERE o.status != 'cancelled'
            ) sub
            ORDER BY rn
            """);
    }

    public async Task<Order> CreateOrderAsync(CreateOrderRequest req, int? userId, string createdByUsername)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var order = await conn.QuerySingleAsync<Order>("""
            WITH inserted AS (
                INSERT INTO orders (order_number, sku_id, production_line_id, volume, uom_id, priority, due_date, cage, cage_size, created_by_id)
                SELECT @orderNumber, s.id, @lineId, @volume, u.id, @priority, @dueDate::date, @cage, @cageSize, @userId
                FROM skus s, uom u
                WHERE s.code = @skuCode AND u.code = @uomCode
                RETURNING id, order_number, sku_id, production_line_id, volume, uom_id, priority, due_date, cage, cage_size, comment
            )
            SELECT
                i.id,
                i.order_number,
                s.code              AS sku,
                i.priority,
                i.volume,
                u.code              AS uom_code,
                i.production_line_id AS line,
                COALESCE(i.due_date::text, '') AS due_date,
                'Queued'            AS sequence,
                i.cage,
                i.cage_size,
                0                   AS produced_packages,
                i.comment
            FROM inserted i
            JOIN skus s ON s.id = i.sku_id
            LEFT JOIN uom u ON u.id = i.uom_id
            """,
            new
            {
                orderNumber = req.OrderNumber,
                skuCode     = req.SkuCode,
                lineId      = req.LineId,
                volume      = req.Volume,
                uomCode     = req.UomCode,
                priority    = req.Priority,
                dueDate     = string.IsNullOrEmpty(req.DueDate) ? null : req.DueDate,
                cage        = req.Cage,
                cageSize    = req.Cage ? req.CageSize : 50,
                userId      = (object?)userId
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
            WHERE id = @orderId AND status != 'cancelled'
            RETURNING order_number
            """,
            new { orderId });

        if (orderNumber != null)
            logger.LogInformation("Order cancelled: #{OrderNumber} (id={Id}) by {User}", orderNumber, orderId, cancelledBy);

        return orderNumber != null;
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
                o.cage,
                o.cage_size,
                o.comment,
                COALESCE((SELECT SUM(packages) FROM cages WHERE order_number = o.order_number), 0)::int AS produced_packages
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
                    c.scanned_at::text AS scanned_at,
                    u.username AS scanned_by
                FROM cages c
                LEFT JOIN users u ON u.id = c.scanned_by_id
                WHERE c.order_number = @orderNumber
                ORDER BY c.scanned_at DESC
                """,
                new { orderNumber = detail.OrderNumber })).ToList();
        }

        return detail;
    }

    public async Task<CageEntry?> ScanCageAsync(string orderNumber, string cageGuid, int cageSize, int? userId)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<CageEntry>("""
            INSERT INTO cages (order_number, cage_guid, cage_size, packages, scanned_by_id)
            VALUES (@orderNumber, @cageGuid::uuid, @cageSize, @cageSize, @userId)
            ON CONFLICT (order_number, cage_guid) DO NOTHING
            RETURNING id, cage_guid::text AS cage_guid, cage_size, packages, scanned_at::text AS scanned_at, NULL AS scanned_by
            """,
            new { orderNumber, cageGuid, cageSize, userId = (object?)userId });
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
}
