using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public interface IUserRepository
{
    Task<(int Id, string Role, string FullName)> UpsertUserAsync(string keycloakId, string email, string username, string fullName, IEnumerable<string> groups);
    Task<(int? Id, string? Role)> GetUserContextAsync(string keycloakId);
    Task<IEnumerable<DbUser>> GetUsersAsync();
    Task UpdateUserRoleAsync(int userId, string newRole);
    Task UpdateUserNameAsync(string keycloakId, string fullName);
    Task<IEnumerable<UserNotificationPref>> GetNotificationPrefsAsync(int userId);
    Task SaveNotificationPrefsAsync(int userId, IEnumerable<UserNotificationPref> prefs);
    Task<DateTime?> GetLastAlertAckAtAsync(int userId);
    Task AckAlertsAsync(int userId);
}
