using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public interface IEventRepository
{
    Task<IEnumerable<ProductionEvent>> GetEventsAsync(int? lineId, string? eventType, string? severity, int limit = 100);
    Task<IEnumerable<UnacknowledgedStop>> GetUnacknowledgedStopsAsync();
    Task<ProductionEvent> CreateEventAsync(CreateEventRequest req, int? userId, string createdBy);
    Task<bool> CloseEventAsync(int id, string? endAt, string? description);
    Task<int> CloseOpenEventsByLineAsync(int lineId);
}
