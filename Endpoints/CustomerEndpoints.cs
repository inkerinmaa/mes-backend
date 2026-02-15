using MyDashboardApi.Models;

namespace MyDashboardApi.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        group.MapGet("/customers", GetCustomers).WithName("GetCustomers");

        return app;
    }

    private static Customer[] GetCustomers()
    {
        var firstNames = new[] { "James", "Mia", "William", "Emma", "Ethan", "Olivia", "Liam", "Sophia", "Noah", "Ava", "Lucas", "Isabella", "Mason", "Charlotte", "Logan" };
        var lastNames = new[] { "Anderson", "White", "Brown", "Davis", "Harris", "Miller", "Wilson", "Moore", "Taylor", "Thomas", "Jackson", "Martin", "Lee", "Garcia", "Clark" };
        var locations = new[] { "New York, NY", "Los Angeles, CA", "Chicago, IL", "Houston, TX", "Phoenix, AZ", "Philadelphia, PA", "San Antonio, TX", "San Diego, CA", "Dallas, TX", "Austin, TX" };
        var statuses = new[] { "subscribed", "unsubscribed", "bounced" };

        return Enumerable.Range(1, 30).Select(i =>
        {
            var first = firstNames[Random.Shared.Next(firstNames.Length)];
            var last = lastNames[Random.Shared.Next(lastNames.Length)];
            return new Customer(
                Id: i,
                Name: $"{first} {last}",
                Email: $"{first.ToLower()}.{last.ToLower()}@example.com",
                Avatar: new Avatar($"https://i.pravatar.cc/150?u={i}", $"{first} {last}"),
                Status: statuses[Random.Shared.Next(statuses.Length)],
                Location: locations[Random.Shared.Next(locations.Length)]);
        }).ToArray();
    }
}
