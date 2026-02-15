using Microsoft.AspNetCore.SignalR;
using MyDashboardApi.Hubs;

namespace MyDashboardApi.Services;

/// <summary>
/// Simulates real-time process data from OPC UA.
///
/// Architecture decision: this service reads DIRECTLY from OPC UA subscriptions,
/// NOT from ClickHouse. ClickHouse is for historical queries only.
///
/// In production, this would:
/// 1. Connect to OPC UA server (e.g., opc.tcp://plc-server:4840)
/// 2. Subscribe to monitored items (temperature, pressure, cycle time, machine state)
/// 3. On each value-change callback, immediately push to SignalR
///
/// A separate OpcUaCollectorService would batch-insert the same data into ClickHouse
/// for historical storage and trend analysis.
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
        _logger.LogInformation("ProcessDataService started (mock mode — simulating OPC UA telemetry)");

        var temperature = 72.0;
        var pressure = 14.7;
        var cycleTime = 45.0;
        var machineStates = new[] { "Running", "Running", "Running", "Idle", "Warning" };

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
        }
    }
}
