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

        return app;
    }

    private static DashboardStats GetStats()
    {
        return new DashboardStats(
            TotalTonnes: new StatMetricDecimal(
                Math.Round(Random.Shared.NextDouble() * 80 + 20, 1),
                Math.Round(Random.Shared.NextDouble() * 20 - 5, 1)),
            LineUptime: new StatMetricDecimal(
                Math.Round(Random.Shared.NextDouble() * 15 + 85, 1),
                Math.Round(Random.Shared.NextDouble() * 10 - 3, 1)),
            WastePercentage: new StatMetricDecimal(
                Math.Round(Random.Shared.NextDouble() * 5 + 1, 1),
                Math.Round(Random.Shared.NextDouble() * 6 - 3, 1)),
            Orders: new StatMetric(Random.Shared.Next(100, 300), Random.Shared.Next(-5, 16)));
    }

    private static List<EfficiencyPoint> GetEfficiency(string? period, string? startDate, string? endDate)
    {
        var start = startDate != null ? DateOnly.Parse(startDate) : DateOnly.FromDateTime(DateTime.Now.AddDays(-14));
        var end = endDate != null ? DateOnly.Parse(endDate) : DateOnly.FromDateTime(DateTime.Now);

        var points = new List<EfficiencyPoint>();
        var current = start;

        while (current <= end)
        {
            var value = Math.Round(Random.Shared.NextDouble() * 25 + 70, 1);
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

    private static ProductionEvent[] GetEvents()
    {
        var events = new[]
        {
            "Order #{0} completed",
            "Batch changeover started",
            "Speed adjusted on Line {1}",
            "Quality check passed",
            "Pallet #{0} dispatched",
            "Material loaded on Line {1}",
            "Operator shift change",
            "Cleaning cycle completed on Line {1}",
            "Order #{0} started production",
            "Packaging run finished"
        };

        var severities = new[] { "info", "info", "info", "warning", "info", "info", "info", "warning", "info", "info" };
        var lines = new[] { "Line 1", "Line 2", "Line 3" };
        var now = DateTime.UtcNow;

        return Enumerable.Range(0, 8).Select(i =>
        {
            var line = lines[Random.Shared.Next(lines.Length)];
            var orderId = Random.Shared.Next(4500, 4700);
            var lineNum = line.Split(' ')[1];
            var eventText = events[Random.Shared.Next(events.Length)]
                .Replace("{0}", orderId.ToString())
                .Replace("{1}", lineNum);

            return new ProductionEvent(
                Id: i + 1,
                Time: now.AddMinutes(-Random.Shared.Next(5, 480)).ToString("o"),
                Event: eventText,
                Line: line,
                Severity: severities[Random.Shared.Next(severities.Length)]);
        })
        .OrderByDescending(e => e.Time)
        .ToArray();
    }

    private static MachineState[] GetStates()
    {
        var states = new[] { "running", "running", "running", "running", "warning", "stopped" };
        var now = DateTime.UtcNow;
        var result = new List<MachineState>();

        var current = now.AddHours(-8);
        while (current < now)
        {
            var state = states[Random.Shared.Next(states.Length)];
            var duration = Random.Shared.Next(10, 90);
            result.Add(new MachineState(
                Timestamp: current.ToString("o"),
                State: state,
                DurationMinutes: duration));
            current = current.AddMinutes(duration);
        }

        return result.ToArray();
    }

    private static Order[] GetCurrentOrders()
    {
        var skus = new[] { "SKU-A100", "SKU-A200", "SKU-B150", "SKU-B300", "SKU-C400" };
        var priorities = new[] { "High", "Medium", "Low" };
        var now = DateTime.UtcNow;

        var sequences = new[] { "Previous", "In Progress", "Next", "Next+1" };

        return sequences.Select((seq, i) => new Order(
            Id: 4500 + i,
            OrderNumber: $"WO-DEMO-{4500 + i}",
            Sku: skus[Random.Shared.Next(skus.Length)],
            Priority: priorities[Random.Shared.Next(priorities.Length)],
            Quantity: Random.Shared.Next(100, 2001),
            Line: Random.Shared.Next(1, 4),
            DueDate: now.AddDays(i - 1).ToString("yyyy-MM-dd"),
            Sequence: seq
        )).ToArray();
    }
}
