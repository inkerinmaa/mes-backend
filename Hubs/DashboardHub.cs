using Microsoft.AspNetCore.SignalR;

namespace MyDashboardApi.Hubs;

public class DashboardHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}
