using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public interface ISkuRepository
{
    Task<IEnumerable<Sku>> GetSkusAsync();
}
