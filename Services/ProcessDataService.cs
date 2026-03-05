using Microsoft.AspNetCore.SignalR;
using MyDashboardApi.Hubs;

namespace MyDashboardApi.Services;

/// <summary>
/// Simulates real-time process data from OPC UA.
///
/// Architecture decision: this service reads DIRECTLY from OPC UA subscriptions,
/// NOT from ClickHouse. ClickHouse is for historical queries only.
///
/// Pushes two SignalR events:
/// - ProcessDataUpdated: raw telemetry (temperature, pressure, cycleTime, machineState)
/// - StatsUpdated: aggregated KPIs (totalTonnes, lineUptime, wastePercentage)
/// </summary>
public class ProcessDataService : BackgroundService
{
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly ILogger<ProcessDataService> _logger;

    public ProcessDataService(IHubContext<DashboardHub> hubContext, ILogger<ProcessDataService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProcessDataService started (mock mode)");

        var temperature = 72.0;
        var pressure = 14.7;
        var cycleTime = 45.0;
        var machineStates = new[] { "Running", "Running", "Running", "Idle", "Warning" };

        var totalTonnes = 42.5;
        var lineUptime = 94.2;
        var wastePercentage = 3.1;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(3000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Simulate OPC UA telemetry
            temperature += Random.Shared.NextDouble() * 4 - 2;
            temperature = Math.Clamp(temperature, 60, 90);

            pressure += Random.Shared.NextDouble() * 1 - 0.5;
            pressure = Math.Clamp(pressure, 12, 18);

            cycleTime += Random.Shared.NextDouble() * 6 - 3;
            cycleTime = Math.Clamp(cycleTime, 30, 60);

            var machineState = machineStates[Random.Shared.Next(machineStates.Length)];

            await _hubContext.Clients.All.SendAsync(
                "ProcessDataUpdated",
                new
                {
                    timestamp = DateTime.UtcNow.ToString("o"),
                    temperature = Math.Round(temperature, 1),
                    pressure = Math.Round(pressure, 2),
                    cycleTime = Math.Round(cycleTime, 1),
                    machineState
                },
                CancellationToken.None);

            // Simulate KPI updates
            totalTonnes += Random.Shared.NextDouble() * 0.5;
            totalTonnes = Math.Round(totalTonnes, 1);

            lineUptime += Random.Shared.NextDouble() * 0.4 - 0.2;
            lineUptime = Math.Round(Math.Clamp(lineUptime, 80, 100), 1);

            wastePercentage += Random.Shared.NextDouble() * 0.4 - 0.2;
            wastePercentage = Math.Round(Math.Clamp(wastePercentage, 0.5, 8), 1);

            await _hubContext.Clients.All.SendAsync(
                "StatsUpdated",
                new
                {
                    totalTonnes,
                    totalTonnesVariation = Math.Round(Random.Shared.NextDouble() * 10 - 2, 1),
                    lineUptime,
                    lineUptimeVariation = Math.Round(Random.Shared.NextDouble() * 6 - 2, 1),
                    wastePercentage,
                    wastePercentageVariation = Math.Round(Random.Shared.NextDouble() * 4 - 2, 1)
                },
                CancellationToken.None);
        }
    }
}
