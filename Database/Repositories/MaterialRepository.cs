using Dapper;
using MyDashboardApi.Models;
using Npgsql;

namespace MyDashboardApi.Database.Repositories;

public class MaterialRepository(NpgsqlDataSource pg) : IMaterialRepository
{
    public async Task<IEnumerable<Material>> GetAllAsync()
    {
        await using var conn = await pg.OpenConnectionAsync();
        return await conn.QueryAsync<Material>(
            "SELECT id, code, name, unit, stock_quantity FROM materials ORDER BY code");
    }
}
