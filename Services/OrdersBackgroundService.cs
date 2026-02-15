using Microsoft.AspNetCore.SignalR;
using MyDashboardApi.Hubs;
using MyDashboardApi.Models;

namespace MyDashboardApi.Services;

/// <summary>
/// Simulates business events from PostgreSQL.
/// In production, this would use Npgsql LISTEN/NOTIFY or poll a PostgreSQL
/// table for order status changes and push updates via SignalR.
/// </summary>
public class OrdersBackgroundService : BackgroundService
{
    private readonly IHubContext<DashboardHub> _hubContext;

    public OrdersBackgroundService(IHubContext<DashboardHub> hubContext)
    {
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var currentOrders = Random.Shared.Next(100, 300);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            currentOrders += Random.Shared.Next(-3, 6);
            currentOrders = Math.Clamp(currentOrders, 50, 500);

            var variation = Random.Shared.Next(-5, 16);

            await _hubContext.Clients.All.SendAsync(
                "OrdersUpdated",
                new StatMetric(currentOrders, variation),
                CancellationToken.None);
        }
    }
}
