using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public interface ISettingsRepository
{
    Task<IEnumerable<Setting>> GetAllAsync();
    Task<Setting?> SetAsync(string key, string value, int? changedById);
}
