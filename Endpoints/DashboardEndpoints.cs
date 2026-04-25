using MyDashboardApi.Database.Repositories;
using MyDashboardApi.Models;

namespace MyDashboardApi.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard").RequireAuthorization();

        group.MapGet("/stats", GetStats).WithName("GetDashboardStats");
        group.MapGet("/efficiency", GetEfficiency).WithName("GetDashboardEfficiency");
        group.MapGet("/events", GetEvents).WithName("GetDashboardEvents");
        group.MapGet("/states", GetStates).WithName("GetDashboardStates");
        group.MapGet("/current-orders", GetCurrentOrders).WithName("GetDashboardCurrentOrders");

        app.MapGet("/api/logs", GetLogs).RequireAuthorization().WithName("GetLogs");

        return app;
    }

    private static async Task<IResult> GetLogs(ILogRepository logs, string? type, string? level, int limit = 20)
    {
        return Results.Ok(await logs.GetRecentAsync(limit, type, level));
    }

    private static DashboardStats GetStats(int lineId = 1)
    {
        // Seed per line so each line shows distinct but stable-ish base values
        var rng = new Random(lineId * 17 + DateTime.UtcNow.Second);
        return new DashboardStats(
            TotalTonnes: new StatMetricDecimal(
                Math.Round(rng.NextDouble() * 60 + (lineId * 15), 1),
                Math.Round(rng.NextDouble() * 20 - 5, 1)),
            LineUptime: new StatMetricDecimal(
                Math.Round(rng.NextDouble() * 10 + (90 - lineId * 2), 1),
                Math.Round(rng.NextDouble() * 10 - 3, 1)),
            WastePercentage: new StatMetricDecimal(
                Math.Round(rng.NextDouble() * 4 + lineId * 0.5, 1),
                Math.Round(rng.NextDouble() * 6 - 3, 1)),
            Orders: new StatMetric(rng.Next(50 + lineId * 30, 150 + lineId * 50), rng.Next(-5, 16)));
    }

    private static List<EfficiencyPoint> GetEfficiency(string? period, string? startDate, string? endDate, int lineId = 1)
    {
        var start = startDate != null ? DateOnly.Parse(startDate) : DateOnly.FromDateTime(DateTime.Now.AddDays(-14));
        var end = endDate != null ? DateOnly.Parse(endDate) : DateOnly.FromDateTime(DateTime.Now);

        // Base efficiency differs per line so charts look distinct when switching
        double baseEfficiency = lineId switch { 1 => 88, 2 => 82, _ => 76 };

        var points = new List<EfficiencyPoint>();
        var current = start;

        while (current <= end)
        {
            var value = Math.Round(Random.Shared.NextDouble() * 15 + baseEfficiency, 1);
            points.Add(new EfficiencyPoint(current.ToString("yyyy-MM-dd"), value));

            current = period switch
            {
                "weekly" => current.AddDays(7),
                "monthly" => current.AddMonths(1),
                _ => current.AddDays(1)
            };
        }

        return points;
    }

    private static async Task<IResult> GetEvents(ILogRepository logs, string? type, string? level, int limit = 5)
    {
        return Results.Ok(await logs.GetRecentAsync(limit, type, level));
    }

    private static async Task<IResult> GetStates(IMachineStateRepository machineStates, int lineId = 1)
    {
        return Results.Ok(await machineStates.GetStatesForLineAsync(lineId));
    }

    private static Order[] GetCurrentOrders()
    {
        var skus = new[] { "SKU-A100", "SKU-A200", "SKU-B150", "SKU-B300", "SKU-C400" };
        var priorities = new[] { "High", "Medium", "Low" };
        var now = DateTime.UtcNow;

        var sequences = new[] { "Previous", "In Progress", "Next", "Next+1" };

        return sequences.Select((seq, i) => new Order
        {
            Id = 4500 + i,
            OrderNumber = $"WO-DEMO-{4500 + i}",
            Sku = skus[Random.Shared.Next(skus.Length)],
            Priority = priorities[Random.Shared.Next(priorities.Length)],
            Volume = Random.Shared.Next(100, 2001),
            UomCode = "pcs",
            Line = Random.Shared.Next(1, 4),
            DueDate = now.AddDays(i - 1).ToString("yyyy-MM-dd"),
            Sequence = seq,
            Cage = false,
            ProducedPackages = 0,
            Comment = null
        }).ToArray();
    }
}
