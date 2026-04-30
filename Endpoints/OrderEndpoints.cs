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
        group.MapGet("/orders/{id:int}", GetOrderDetail).WithName("GetOrderDetail");
        group.MapPost("/orders/{id:int}/cages", AddCage).WithName("AddCage");
        group.MapPatch("/orders/{id:int}/cages/{cageId:int}/packages", UpdateCagePackages).WithName("UpdateCagePackages");
        group.MapDelete("/orders/{id:int}/cages/{cageId:int}", DeleteCage).WithName("DeleteCage");
        group.MapPatch("/orders/{id:int}/comment", UpdateComment).WithName("UpdateComment");
        group.MapPatch("/orders/{id:int}/status", TransitionStatus).WithName("TransitionOrderStatus");
        group.MapPatch("/orders/{id:int}/resequence", ResequenceOrder).WithName("ResequenceOrder");
        group.MapPatch("/orders/{id:int}/schedule", RescheduleOrder).WithName("RescheduleOrder");
        return app;
    }

    private static async Task<IResult> GetOrders(IOrderRepository orders)
    {
        return Results.Ok(await orders.GetOrdersAsync());
    }

    private static async Task<IResult> GetOrderDetail(int id, IOrderRepository orders)
    {
        var detail = await orders.GetOrderDetailAsync(id);
        return detail is null ? Results.NotFound(new { error = "Order not found" }) : Results.Ok(detail);
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

    private static async Task<IResult> AddCage(
        int id, IOrderRepository orders,
        IUserRepository users, HttpContext ctx, ILogger<Program> logger)
    {
        var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
        var (userId, _) = await users.GetUserContextAsync(keycloakId);

        var detail = await orders.GetOrderDetailAsync(id);
        if (detail is null) return Results.NotFound(new { error = "Order not found" });

        if (!detail.Cage)
            return Results.BadRequest(new { error = "This order does not use cage tracking" });

        var cage = await orders.AddCageAsync(detail.OrderNumber, detail.CageSize, userId);
        logger.LogInformation("Cage added for order {OrderNumber}", detail.OrderNumber);
        return Results.Created($"/api/orders/{id}/cages/{cage!.Id}", cage);
    }

    private static async Task<IResult> UpdateCagePackages(
        int id, int cageId, UpdateCagePackagesRequest req, IOrderRepository orders)
    {
        if (req.Packages <= 0)
            return Results.BadRequest(new { error = "Packages must be greater than 0" });

        var updated = await orders.UpdateCagePackagesAsync(cageId, req.Packages);
        return updated ? Results.Ok(new { cageId, packages = req.Packages }) : Results.NotFound(new { error = "Cage not found" });
    }

    private static async Task<IResult> DeleteCage(int id, int cageId, IOrderRepository orders)
    {
        var deleted = await orders.DeleteCageAsync(cageId);
        return deleted ? Results.Ok(new { cageId }) : Results.NotFound(new { error = "Cage not found" });
    }

    private static async Task<IResult> UpdateComment(
        int id, UpdateCommentRequest req, IOrderRepository orders)
    {
        var updated = await orders.UpdateOrderCommentAsync(id, req.Comment);
        return updated ? Results.Ok(new { id, comment = req.Comment }) : Results.NotFound(new { error = "Order not found" });
    }

    private static async Task<IResult> RescheduleOrder(
        int id, RescheduleOrderRequest req, IOrderRepository orders,
        IUserRepository users, HttpContext ctx)
    {
        var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
        var (_, role) = await users.GetUserContextAsync(keycloakId);
        if (role != "admin") return Results.Forbid();

        var updated = await orders.RescheduleOrderAsync(id, req.PlannedStartAt, req.PlannedCompleteAt);
        return updated
            ? Results.Ok(new { id })
            : Results.NotFound(new { error = "Order not found or already completed/cancelled" });
    }

    private static async Task<IResult> ResequenceOrder(
        int id, ResequenceOrderRequest req, IOrderRepository orders,
        IUserRepository users, HttpContext ctx)
    {
        var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
        var (_, role) = await users.GetUserContextAsync(keycloakId);
        if (role != "admin") return Results.Forbid();

        if (req.Direction != "up" && req.Direction != "down")
            return Results.BadRequest(new { error = "Direction must be 'up' or 'down'" });

        var moved = await orders.ResequenceOrderAsync(id, req.Direction);
        return moved
            ? Results.Ok(new { id })
            : Results.BadRequest(new { error = "Cannot move order further in that direction" });
    }

    private static async Task<IResult> TransitionStatus(
        int id, TransitionStatusRequest req, IOrderRepository orders,
        IUserRepository users, HttpContext ctx, ILogger<Program> logger)
    {
        var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
        var username   = ctx.User.FindFirst("preferred_username")?.Value ?? keycloakId;

        var (_, role) = await users.GetUserContextAsync(keycloakId);
        if (role != "admin")
        {
            logger.LogWarning("Unauthorized status transition attempt on order {Id} by {Username}", id, username);
            return Results.Forbid();
        }

        var (success, error) = await orders.TransitionStatusAsync(id, req.Action, username);
        if (!success)
        {
            logger.LogWarning("Status transition failed for order {Id}: {Error}", id, error);
            return Results.BadRequest(new { error });
        }
        return Results.Ok(new { id, action = req.Action });
    }
}
