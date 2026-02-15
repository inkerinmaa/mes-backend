using MyDashboardApi.Models;

namespace MyDashboardApi.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard").RequireAuthorization();

        group.MapGet("/stats", GetStats).WithName("GetDashboardStats");
        group.MapGet("/revenue", GetRevenue).WithName("GetDashboardRevenue");
        group.MapGet("/sales", GetSales).WithName("GetDashboardSales");

        return app;
    }

    private static DashboardStats GetStats()
    {
        return new DashboardStats(
            Customers: new StatMetric(Random.Shared.Next(400, 1000), Random.Shared.Next(-15, 26)),
            Conversions: new StatMetric(Random.Shared.Next(1000, 2000), Random.Shared.Next(-10, 21)),
            Revenue: new StatMetric(Random.Shared.Next(200000, 500000), Random.Shared.Next(-20, 31)),
            Orders: new StatMetric(Random.Shared.Next(100, 300), Random.Shared.Next(-5, 16)));
    }

    private static List<RevenuePoint> GetRevenue(string? period, string? startDate, string? endDate)
    {
        var start = startDate != null ? DateOnly.Parse(startDate) : DateOnly.FromDateTime(DateTime.Now.AddDays(-14));
        var end = endDate != null ? DateOnly.Parse(endDate) : DateOnly.FromDateTime(DateTime.Now);

        var points = new List<RevenuePoint>();
        var current = start;

        while (current <= end)
        {
            points.Add(new RevenuePoint(current.ToString("yyyy-MM-dd"), Random.Shared.Next(1000, 10001)));

            current = period switch
            {
                "weekly" => current.AddDays(7),
                "monthly" => current.AddMonths(1),
                _ => current.AddDays(1)
            };
        }

        return points;
    }

    private static Sale[] GetSales()
    {
        var emails = new[] { "james.anderson@example.com", "mia.white@example.com", "william.brown@example.com", "emma.davis@example.com", "ethan.harris@example.com" };
        var statuses = new[] { "paid", "failed", "refunded" };
        var now = DateTime.UtcNow;

        return Enumerable.Range(0, 5).Select(i => new Sale(
            Id: (4600 - i).ToString(),
            Date: now.AddHours(-Random.Shared.Next(0, 48)).ToString("o"),
            Status: statuses[Random.Shared.Next(statuses.Length)],
            Email: emails[Random.Shared.Next(emails.Length)],
            Amount: Random.Shared.Next(100, 1001)))
            .OrderByDescending(s => s.Date)
            .ToArray();
    }
}
