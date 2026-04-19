using Dapper;
using Npgsql;
using Microsoft.Extensions.Logging;
using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public class UserRepository(NpgsqlDataSource dataSource, ILogger<UserRepository> logger) : IUserRepository
{
    private class UpsertResult
    {
        public int Id { get; set; }
        public string Role { get; set; } = "";
        public bool IsNew { get; set; }
    }

    private class UserContext
    {
        public int Id { get; set; }
        public string Role { get; set; } = "";
    }

    public async Task<(int Id, string Role)> UpsertUserAsync(
        string keycloakId, string email, string username, string fullName)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        // First user ever to log in gets admin; subsequent new users get viewer
        var result = await conn.QuerySingleAsync<UpsertResult>("""
            INSERT INTO users (keycloak_id, email, username, full_name, last_login, role)
            VALUES (
                @keycloakId, @email, @username, @fullName, NOW(),
                CASE WHEN NOT EXISTS (SELECT 1 FROM users WHERE role = 'admin') THEN 'admin' ELSE 'viewer' END
            )
            ON CONFLICT (keycloak_id) DO UPDATE
                SET email     = EXCLUDED.email,
                    username  = EXCLUDED.username,
                    full_name = EXCLUDED.full_name,
                    last_login = NOW()
            RETURNING id, role, (xmax = 0) AS is_new
            """,
            new
            {
                keycloakId,
                email    = string.IsNullOrEmpty(email)    ? null : email,
                username = string.IsNullOrEmpty(username) ? null : username,
                fullName = string.IsNullOrEmpty(fullName) ? null : fullName
            });

        if (result.IsNew)
            logger.LogInformation("New user registered: {Username} | Role={Role}", username, result.Role);
        else
            logger.LogInformation("User synced: {Username} | Role={Role} | LastLogin=NOW", username, result.Role);

        return (result.Id, result.Role);
    }

    public async Task<(int? Id, string? Role)> GetUserContextAsync(string keycloakId)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var result = await conn.QuerySingleOrDefaultAsync<UserContext>(
            "SELECT id, role FROM users WHERE keycloak_id = @keycloakId",
            new { keycloakId });

        return result == null ? (null, null) : (result.Id, result.Role);
    }

    public async Task<IEnumerable<DbUser>> GetUsersAsync()
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QueryAsync<DbUser>("""
            SELECT
                id,
                COALESCE(username,  '') AS username,
                COALESCE(full_name, '') AS full_name,
                COALESCE(email,     '') AS email,
                role,
                TO_CHAR(last_login AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI') AS last_login
            FROM users
            ORDER BY last_login DESC
            """);
    }

    public async Task UpdateUserRoleAsync(int userId, string newRole)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var username = await conn.ExecuteScalarAsync<string?>(
            "UPDATE users SET role = @newRole WHERE id = @userId RETURNING username",
            new { newRole, userId });

        logger.LogInformation("Role updated: user {Username} (id={Id}) → {Role}", username, userId, newRole);
    }
}
