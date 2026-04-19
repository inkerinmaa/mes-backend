using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public interface IOrderRepository
{
    Task<IEnumerable<Order>> GetOrdersAsync();
    Task<Order> CreateOrderAsync(CreateOrderRequest req, int? userId, string createdByUsername);
    Task<bool> CancelOrderAsync(int orderId, string cancelledBy);
}
