using Dapper;
using Npgsql;
using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public class SkuRepository(NpgsqlDataSource dataSource) : ISkuRepository
{
    public async Task<IEnumerable<Sku>> GetSkusAsync()
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QueryAsync<Sku>("SELECT id, code, name, unit FROM skus ORDER BY code");
    }
}
