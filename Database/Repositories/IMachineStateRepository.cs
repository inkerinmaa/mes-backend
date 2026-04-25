using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public interface IMachineStateRepository
{
    Task<IEnumerable<MachineState>> GetStatesForLineAsync(int lineId);
    Task InsertStateAsync(int lineId, string state);
}
