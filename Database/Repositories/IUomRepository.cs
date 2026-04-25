using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public interface IUomRepository
{
    Task<IEnumerable<Uom>> GetUomsAsync();
}
