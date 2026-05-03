namespace MyDashboardApi.Database.Repositories;

public interface IProcessRepository
{
    Task<IEnumerable<ProcessParam>> GetLatestAsync(int lineId, string unit);
}

public record ProcessParam(string Param, float Value, string Ts);
