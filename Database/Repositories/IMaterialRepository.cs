using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public interface IMaterialRepository
{
    Task<IEnumerable<Material>> GetAllAsync();
}
