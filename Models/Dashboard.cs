namespace MyDashboardApi.Models;

public record StatMetric(int Value, int Variation);

public record DashboardStats(
    StatMetric Customers,
    StatMetric Conversions,
    StatMetric Revenue,
    StatMetric Orders);

public record RevenuePoint(string Date, int Amount);

public record Sale(string Id, string Date, string Status, string Email, int Amount);
