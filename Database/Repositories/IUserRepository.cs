using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public interface IUserRepository
{
    Task<(int Id, string Role)> UpsertUserAsync(string keycloakId, string email, string username, string fullName);
    Task<(int? Id, string? Role)> GetUserContextAsync(string keycloakId);
    Task<IEnumerable<DbUser>> GetUsersAsync();
    Task UpdateUserRoleAsync(int userId, string newRole);
}
