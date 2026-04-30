using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MyDashboardApi.Database.Repositories;
using MyDashboardApi.Hubs;

namespace MyDashboardApi.Endpoints;

public static class MachineStateEndpoints
{
    private static readonly HashSet<string> _validStates = ["running", "warning", "stopped"];

    public static void MapMachineStateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/machine-states").RequireAuthorization();

        group.MapPost("/", async (
            [FromBody] MachineStateRequest req,
            IMachineStateRepository repo,
            IHubContext<DashboardHub> hub) =>
        {
            if (!_validStates.Contains(req.State))
                return Results.BadRequest($"State must be one of: {string.Join(", ", _validStates)}");

            await repo.InsertStateAsync(req.LineId, req.State);
            await hub.Clients.All.SendAsync("MachineStateUpdated", new { lineId = req.LineId });
            if (req.State == "stopped")
                await hub.Clients.All.SendAsync("StopInserted", new { lineId = req.LineId });
            return Results.Ok();
        });
    }
}

public record MachineStateRequest(int LineId, string State);
