using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public interface IProductRepository
{
    Task<IEnumerable<ProductListItem>> GetProductsAsync();
    Task<ProductDetail?> GetProductDetailAsync(int id);
    Task<bool> UpdateProductAsync(int id, UpdateProductRequest req, int userId);
}
