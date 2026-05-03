using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using NATS.Client.Core;
using MyDashboardApi.Database.Repositories;
using MyDashboardApi.Hubs;

namespace MyDashboardApi.Services;

public class NatsLineStateService : BackgroundService
{
    private static readonly HashSet<string> _validStates = ["running", "warning", "stopped"];

    private readonly IConfiguration _config;
    private readonly IMachineStateRepository _machineStateRepo;
    private readonly IEventRepository _eventRepo;
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly ILogger<NatsLineStateService> _logger;

    public NatsLineStateService(
        IConfiguration config,
        IMachineStateRepository machineStateRepo,
        IEventRepository eventRepo,
        IHubContext<DashboardHub> hubContext,
        ILogger<NatsLineStateService> logger)
    {
        _config = config;
        _machineStateRepo = machineStateRepo;
        _eventRepo = eventRepo;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var natsUrl = _config["Nats:Url"] ?? "nats://localhost:4222";
        _logger.LogInformation("NatsLineStateService starting, connecting to {Url}", natsUrl);

        var opts = NatsOpts.Default with { Url = natsUrl };
        await using var nats = new NatsConnection(opts);

        try
        {
            await foreach (var msg in nats.SubscribeAsync<string>("lines.*.state", cancellationToken: stoppingToken))
            {
                if (msg.Data is null) continue;
                try
                {
                    var payload = JsonSerializer.Deserialize<LineStatePayload>(msg.Data,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (payload is null || !_validStates.Contains(payload.State))
                    {
                        _logger.LogWarning("Invalid NATS payload: {Data}", msg.Data);
                        continue;
                    }

                    await _machineStateRepo.InsertStateAsync(payload.LineId, payload.State);
                    if (payload.State != "stopped")
                        await _eventRepo.CloseOpenEventsByLineAsync(payload.LineId);
                    await _hubContext.Clients.All.SendAsync("MachineStateUpdated",
                        new { lineId = payload.LineId }, CancellationToken.None);
                    if (payload.State == "stopped")
                        await _hubContext.Clients.All.SendAsync("StopInserted",
                            new { lineId = payload.LineId }, CancellationToken.None);

                    _logger.LogInformation("Line {LineId} state → {State} (via NATS)", payload.LineId, payload.State);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process NATS message: {Data}", msg.Data);
                }
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NatsLineStateService terminated unexpectedly");
        }
    }

    private record LineStatePayload(int LineId, string State);
}
