using MyDashboardApi.Database.Repositories;
using MyDashboardApi.Models;

namespace MyDashboardApi.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        group.MapGet("/notifications", GetAlerts).WithName("GetAlerts");
        group.MapPost("/notifications/ack", AckAlerts).WithName("AckAlerts");
        group.MapGet("/me/notification-prefs", GetNotificationPrefs).WithName("GetNotificationPrefs");
        group.MapPut("/me/notification-prefs", SaveNotificationPrefs).WithName("SaveNotificationPrefs");

        return app;
    }

    private static async Task<IResult> GetAlerts(
        IUserRepository users, ILogRepository logs, HttpContext ctx)
    {
        var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
        var (userId, _) = await users.GetUserContextAsync(keycloakId);
        if (userId == null) return Results.Ok(Array.Empty<LogEntry>());

        var prefs   = await users.GetNotificationPrefsAsync(userId.Value);
        var since   = await users.GetLastAlertAckAtAsync(userId.Value);
        var enabled = prefs.Where(p => p.Enabled).Select(p => p.LogType);

        return Results.Ok(await logs.GetAlertLogsAsync(enabled, since));
    }

    private static async Task<IResult> AckAlerts(
        IUserRepository users, HttpContext ctx)
    {
        var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
        var (userId, _) = await users.GetUserContextAsync(keycloakId);
        if (userId == null) return Results.NotFound(new { error = "User not found" });

        await users.AckAlertsAsync(userId.Value);
        return Results.Ok();
    }

    private static async Task<IResult> GetNotificationPrefs(
        IUserRepository users, HttpContext ctx)
    {
        var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
        var (userId, _) = await users.GetUserContextAsync(keycloakId);
        if (userId == null) return Results.NotFound(new { error = "User not found" });

        return Results.Ok(await users.GetNotificationPrefsAsync(userId.Value));
    }

    private static async Task<IResult> SaveNotificationPrefs(
        UpdateNotificationPrefsRequest req, IUserRepository users, HttpContext ctx)
    {
        var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
        var (userId, _) = await users.GetUserContextAsync(keycloakId);
        if (userId == null) return Results.NotFound(new { error = "User not found" });

        await users.SaveNotificationPrefsAsync(userId.Value, req.Prefs);
        return Results.Ok(await users.GetNotificationPrefsAsync(userId.Value));
    }
}
