using MyDashboardApi.Models;

namespace MyDashboardApi.Endpoints;

public static class MailEndpoints
{
    public static IEndpointRouteBuilder MapMailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        group.MapGet("/mails", GetMails).WithName("GetMails");

        return app;
    }

    private static Mail[] GetMails()
    {
        var senders = new[]
        {
            new MailSender(101, "Jane Smith", "jane.smith@example.com", new Avatar("https://i.pravatar.cc/150?u=sender101", "Jane Smith"), "subscribed", "Los Angeles, CA"),
            new MailSender(102, "Mike Johnson", "mike.johnson@example.com", new Avatar("https://i.pravatar.cc/150?u=sender102", "Mike Johnson"), "subscribed", "Chicago, IL"),
            new MailSender(103, "Sarah Connor", "sarah.connor@example.com", new Avatar("https://i.pravatar.cc/150?u=sender103", "Sarah Connor"), "subscribed", "Dallas, TX"),
            new MailSender(104, "Tom Hardy", "tom.hardy@example.com", new Avatar("https://i.pravatar.cc/150?u=sender104", "Tom Hardy"), "unsubscribed", "New York, NY")
        };

        var subjects = new[] { "Project Update", "Meeting Tomorrow", "Invoice #1234", "Quick Question", "Weekly Report", "New Feature Request", "Bug Report", "Documentation Review" };
        var bodies = new[]
        {
            "Hi team, here's the latest update on the project. We've made significant progress on the frontend implementation and the API endpoints are now fully functional.",
            "Just a reminder that we have a team meeting scheduled for tomorrow at 10 AM. Please come prepared with your weekly status updates.",
            "Please find attached the invoice for this month's services. Let me know if you have any questions about the billing.",
            "I had a quick question about the deployment process. Could you walk me through the steps when you get a chance?",
            "Here's the weekly report summarizing our progress. Key highlights include the completion of the auth module and initial SignalR integration.",
            "I'd like to propose a new feature for the dashboard - real-time notifications for production line alerts. What do you think?",
            "Found a bug in the order processing module. When submitting orders with special characters, the validation fails silently.",
            "Could you review the updated documentation for the API endpoints? I've added examples for all the new dashboard routes."
        };

        var now = DateTime.UtcNow;

        return Enumerable.Range(1, 8).Select(i =>
        {
            var sender = senders[Random.Shared.Next(senders.Length)];
            return new Mail(
                Id: i,
                Unread: i <= 3,
                From: sender,
                Subject: subjects[i - 1],
                Body: bodies[i - 1],
                Date: now.AddHours(-Random.Shared.Next(1, 72)).ToString("o"));
        })
        .OrderByDescending(m => m.Date)
        .ToArray();
    }
}
