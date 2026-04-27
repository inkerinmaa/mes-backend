using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public interface ILineRepository
{
    Task<IEnumerable<ProductionLine>> GetLinesAsync();
}
