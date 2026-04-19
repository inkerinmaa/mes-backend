using MyDashboardApi.Database.Repositories;
using MyDashboardApi.Models;
using Microsoft.Extensions.Logging;

namespace MyDashboardApi.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();
        group.MapGet("/orders", GetOrders).WithName("GetOrders");
        group.MapPost("/orders", CreateOrder).WithName("CreateOrder");
        group.MapDelete("/orders/{id:int}", CancelOrder).WithName("CancelOrder");
        return app;
    }

    private static async Task<IResult> GetOrders(IOrderRepository orders)
    {
        return Results.Ok(await orders.GetOrdersAsync());
    }

    private static async Task<IResult> CreateOrder(
        CreateOrderRequest req, IOrderRepository orders, IUserRepository users,
        HttpContext ctx, ILogger<Program> logger)
    {
        var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
        var username   = ctx.User.FindFirst("preferred_username")?.Value ?? keycloakId;

        var (userId, role) = await users.GetUserContextAsync(keycloakId);
        if (role != "admin")
        {
            logger.LogWarning("Unauthorized order creation attempt by {Username} (role={Role})", username, role ?? "unknown");
            return Results.Forbid();
        }

        try
        {
            var order = await orders.CreateOrderAsync(req, userId, username);
            return Results.Created($"/api/orders/{order.Id}", order);
        }
        catch (Exception ex) when (ex.Message.Contains("unique") || ex.Message.Contains("duplicate") || ex.Message.Contains("23505"))
        {
            logger.LogWarning("Duplicate order number attempted: {OrderNumber} by {Username}", req.OrderNumber, username);
            return Results.Conflict(new { error = "Order number already exists" });
        }
    }

    private static async Task<IResult> CancelOrder(
        int id, IOrderRepository orders, IUserRepository users,
        HttpContext ctx, ILogger<Program> logger)
    {
        var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
        var username   = ctx.User.FindFirst("preferred_username")?.Value ?? keycloakId;

        var (_, role) = await users.GetUserContextAsync(keycloakId);
        if (role != "admin")
        {
            logger.LogWarning("Unauthorized cancel attempt on order {Id} by {Username}", id, username);
            return Results.Forbid();
        }

        var cancelled = await orders.CancelOrderAsync(id, username);
        return cancelled ? Results.Ok(new { id }) : Results.NotFound(new { error = "Order not found" });
    }
}
