using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public interface IMachineStateRepository
{
    Task<IEnumerable<MachineState>> GetStatesForLineAsync(int lineId, DateTimeOffset from);
    Task<int> InsertStateAsync(int lineId, string state);
}
