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
                quantity,
                line,
                due_date,
                CASE rn
                    WHEN 1 THEN 'In Progress'
                    WHEN 2 THEN 'Next'
                    ELSE 'Next+' || (rn - 2)::text
                END AS sequence
            FROM (
                SELECT
                    o.id,
                    o.order_number,
                    s.code          AS sku,
                    o.priority,
                    o.quantity_packages AS quantity,
                    o.production_line_id AS line,
                    COALESCE(o.due_date::text, '') AS due_date,
                    ROW_NUMBER() OVER (ORDER BY o.created_at) AS rn
                FROM orders o
                JOIN skus s ON s.id = o.sku_id
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
                INSERT INTO orders (order_number, sku_id, production_line_id, quantity_packages, priority, due_date, created_by_id)
                SELECT @orderNumber, s.id, @lineId, @qty, @priority, @dueDate::date, @userId
                FROM skus s WHERE s.code = @skuCode
                RETURNING id, order_number, sku_id, production_line_id, quantity_packages, priority, due_date
            )
            SELECT
                i.id,
                i.order_number,
                s.code              AS sku,
                i.priority,
                i.quantity_packages AS quantity,
                i.production_line_id AS line,
                COALESCE(i.due_date::text, '') AS due_date,
                'Queued'            AS sequence
            FROM inserted i
            JOIN skus s ON s.id = i.sku_id
            """,
            new
            {
                orderNumber = req.OrderNumber,
                skuCode     = req.SkuCode,
                lineId      = req.LineId,
                qty         = req.QuantityPackages,
                priority    = req.Priority,
                dueDate     = string.IsNullOrEmpty(req.DueDate) ? null : req.DueDate,
                userId      = (object?)userId
            });

        logger.LogInformation(
            "Order created: #{OrderNumber} | SKU={Sku} | Line={Line} | Qty={Qty} | Priority={Priority} | By={User}",
            order.OrderNumber, order.Sku, order.Line, order.Quantity, order.Priority, createdByUsername);

        return order;
    }

    public async Task<bool> CancelOrderAsync(int orderId, string cancelledBy)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var orderNumber = await conn.ExecuteScalarAsync<string?>("""
            UPDATE orders SET status = 'cancelled'
            WHERE id = @orderId AND status != 'cancelled'
            RETURNING order_number
            """,
            new { orderId });

        if (orderNumber != null)
            logger.LogInformation("Order cancelled: #{OrderNumber} (id={Id}) by {User}", orderNumber, orderId, cancelledBy);

        return orderNumber != null;
    }
}
