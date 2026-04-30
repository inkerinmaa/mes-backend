using MyDashboardApi.Database.Repositories;
using MyDashboardApi.Models;

namespace MyDashboardApi.Endpoints;

public static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events").RequireAuthorization();

        group.MapGet("/",                GetEvents).WithName("GetEvents");
        group.MapGet("/unacknowledged",  GetUnacknowledged).WithName("GetUnacknowledgedStops");
        group.MapPost("/",               CreateEvent).WithName("CreateEvent");
        group.MapPatch("/{id:int}/close", CloseEvent).WithName("CloseEvent");

        return app;
    }

    private static async Task<IResult> GetEvents(
        IEventRepository events,
        int? lineId, string? eventType, string? severity, int limit = 100)
    {
        return Results.Ok(await events.GetEventsAsync(lineId, eventType, severity, limit));
    }

    private static async Task<IResult> GetUnacknowledged(IEventRepository events)
    {
        return Results.Ok(await events.GetUnacknowledgedStopsAsync());
    }

    private static async Task<IResult> CreateEvent(
        CreateEventRequest req,
        IEventRepository events,
        IUserRepository users,
        HttpContext ctx)
    {
        var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
        var username   = ctx.User.FindFirst("preferred_username")?.Value ?? keycloakId;
        var (userId, _) = await users.GetUserContextAsync(keycloakId);

        if (string.IsNullOrWhiteSpace(req.Title))
            return Results.BadRequest(new { error = "Title is required" });

        var validTypes = new[] { "downtime_unplanned", "downtime_planned", "changeover", "quality_hold", "maintenance", "operator_note", "safety" };
        if (!validTypes.Contains(req.EventType))
            return Results.BadRequest(new { error = $"Invalid event type. Must be one of: {string.Join(", ", validTypes)}" });

        var validSeverities = new[] { "info", "warning", "critical" };
        if (!validSeverities.Contains(req.Severity))
            return Results.BadRequest(new { error = "Severity must be info, warning, or critical" });

        var created = await events.CreateEventAsync(req, userId, username);
        return Results.Created($"/api/events/{created.Id}", created);
    }

    private static async Task<IResult> CloseEvent(
        int id,
        CloseEventRequest req,
        IEventRepository events)
    {
        var closed = await events.CloseEventAsync(id, req.EndAt, req.Description);
        return closed
            ? Results.Ok(new { id })
            : Results.NotFound(new { error = "Event not found or already closed" });
    }
}
