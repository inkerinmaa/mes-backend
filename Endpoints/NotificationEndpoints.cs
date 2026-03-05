using MyDashboardApi.Models;

namespace MyDashboardApi.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        group.MapGet("/notifications", GetNotifications).WithName("GetNotifications");

        return app;
    }

    private static Notification[] GetNotifications()
    {
        var senders = new[]
        {
            new NotificationSender(201, "Line 1 Monitor", "line1@mes.local", new Avatar("https://i.pravatar.cc/150?u=line1", "Line 1"), "active", "Production Floor"),
            new NotificationSender(202, "Line 2 Monitor", "line2@mes.local", new Avatar("https://i.pravatar.cc/150?u=line2", "Line 2"), "active", "Production Floor"),
            new NotificationSender(203, "Line 3 Monitor", "line3@mes.local", new Avatar("https://i.pravatar.cc/150?u=line3", "Line 3"), "active", "Production Floor")
        };

        var bodies = new[]
        {
            "Temperature exceeded 85\u00b0C on Line 1 extruder zone 3.",
            "Pressure dropped below 13 PSI on Line 2 hydraulic system.",
            "Cycle time anomaly detected on Line 3 \u2014 15% above target.",
            "Motor vibration warning on Line 1 main drive.",
            "Conveyor belt speed deviation on Line 2 \u2014 automatic correction applied."
        };

        var now = DateTime.UtcNow;

        return Enumerable.Range(1, 5).Select(i => new Notification(
            Id: i,
            Unread: i <= 2,
            Sender: senders[Random.Shared.Next(senders.Length)],
            Body: bodies[i - 1],
            Date: now.AddMinutes(-Random.Shared.Next(5, 120)).ToString("o")))
            .OrderByDescending(n => n.Date)
            .ToArray();
    }
}
