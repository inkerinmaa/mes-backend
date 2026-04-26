using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public interface IOrderRepository
{
    Task<IEnumerable<Order>> GetOrdersAsync();
    Task<Order> CreateOrderAsync(CreateOrderRequest req, int? userId, string createdByUsername);
    Task<bool> CancelOrderAsync(int orderId, string cancelledBy);
    Task<OrderDetail?> GetOrderDetailAsync(int orderId);
    Task<CageEntry?> ScanCageAsync(string orderNumber, string cageGuid, int cageSize, int? userId);
    Task<bool> UpdateCagePackagesAsync(int cageId, int packages);
    Task<bool> UpdateOrderCommentAsync(int orderId, string? comment);
    Task<bool> DeleteCageAsync(int cageId);
    Task<(bool Success, string? Error)> TransitionStatusAsync(int orderId, string action, string username);
}
