using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public interface ILogRepository
{
    Task WriteAsync(string type, string level, string message);
    Task<IEnumerable<LogEntry>> GetRecentAsync(int limit = 20, string? type = null, string? level = null);
}
