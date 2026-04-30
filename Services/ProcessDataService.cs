using Microsoft.AspNetCore.SignalR;
using MyDashboardApi.Database.Repositories;
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
    private readonly IMachineStateRepository _machineStateRepo;
    private readonly ILogger<ProcessDataService> _logger;

    // Weighted pool — running appears most often to match realistic uptime
    private static readonly string[] _statePool = ["running", "running", "running", "running", "warning", "warning", "stopped"];
    private readonly string[] _lineStates = ["running", "running", "running"];
    private int _tickCount = 0;

    public ProcessDataService(
        IHubContext<DashboardHub> hubContext,
        IMachineStateRepository machineStateRepo,
        ILogger<ProcessDataService> logger)
    {
        _hubContext = hubContext;
        _machineStateRepo = machineStateRepo;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProcessDataService started (mock mode)");

        var temperature = 72.0;
        var pressure = 14.7;
        var cycleTime = 45.0;

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

            // Every 10 ticks (~30 s) randomly transition one line to a new state
            _tickCount++;
            if (_tickCount % 1200 == 0)
            {
                var lineIndex = Random.Shared.Next(3);
                var newState = _statePool[Random.Shared.Next(_statePool.Length)];
                if (newState != _lineStates[lineIndex])
                {
                    _lineStates[lineIndex] = newState;
                    try
                    {
                        await _machineStateRepo.InsertStateAsync(lineIndex + 1, newState);
                        await _hubContext.Clients.All.SendAsync(
                            "MachineStateUpdated",
                            new { lineId = lineIndex + 1 },
                            CancellationToken.None);
                        if (newState == "stopped")
                            await _hubContext.Clients.All.SendAsync(
                                "StopInserted",
                                new { lineId = lineIndex + 1 },
                                CancellationToken.None);
                        _logger.LogInformation("Line {Line} state changed to {State}", lineIndex + 1, newState);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to record machine state change");
                    }
                }
            }

            var machineState = _lineStates[0];

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
