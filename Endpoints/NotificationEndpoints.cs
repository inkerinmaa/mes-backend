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
            new NotificationSender(201, "Alex Turner", "alex.turner@example.com", new Avatar("https://i.pravatar.cc/150?u=alex", "Alex Turner"), "subscribed", "Seattle, WA"),
            new NotificationSender(202, "Rachel Green", "rachel.green@example.com", new Avatar("https://i.pravatar.cc/150?u=rachel", "Rachel Green"), "subscribed", "Boston, MA"),
            new NotificationSender(203, "David Kim", "david.kim@example.com", new Avatar("https://i.pravatar.cc/150?u=david", "David Kim"), "subscribed", "Portland, OR")
        };

        var bodies = new[]
        {
            "Mentioned you in a comment on the production report.",
            "Assigned you a new task: Review Q4 metrics.",
            "Updated the shipping schedule for next week.",
            "Completed the maintenance checklist for Line A.",
            "Requested approval for overtime on Saturday."
        };

        var now = DateTime.UtcNow;

        return Enumerable.Range(1, 5).Select(i => new Notification(
            Id: i,
            Unread: i <= 2,
            Sender: senders[Random.Shared.Next(senders.Length)],
            Body: bodies[i - 1],
            Date: now.AddHours(-Random.Shared.Next(1, 48)).ToString("o")))
            .OrderByDescending(n => n.Date)
            .ToArray();
    }
}
