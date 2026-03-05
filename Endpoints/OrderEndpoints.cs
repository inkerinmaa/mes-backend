using MyDashboardApi.Models;

namespace MyDashboardApi.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        group.MapGet("/orders", GetOrders).WithName("GetOrders");

        return app;
    }

    private static Order[] GetOrders()
    {
        var skus = new[] { "SKU-A100", "SKU-A200", "SKU-B150", "SKU-B300", "SKU-C400", "SKU-C500", "SKU-D100", "SKU-D250", "SKU-E350", "SKU-E500" };
        var priorities = new[] { "High", "Medium", "Low" };
        var now = DateTime.UtcNow;

        return Enumerable.Range(1, 20).Select(i =>
        {
            var sequence = i switch
            {
                1 => "In Progress",
                2 => "Next",
                _ => $"Next+{i - 2}"
            };

            return new Order(
                Id: 4500 + i,
                Sku: skus[Random.Shared.Next(skus.Length)],
                Priority: priorities[Random.Shared.Next(priorities.Length)],
                Quantity: Random.Shared.Next(50, 2001),
                Line: Random.Shared.Next(1, 4),
                DueDate: now.AddDays(Random.Shared.Next(0, 14)).ToString("yyyy-MM-dd"),
                Sequence: sequence);
        }).ToArray();
    }
}
