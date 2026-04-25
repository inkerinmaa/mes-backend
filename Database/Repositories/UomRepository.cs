using Dapper;
using Npgsql;
using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public class UomRepository(NpgsqlDataSource dataSource) : IUomRepository
{
    public async Task<IEnumerable<Uom>> GetUomsAsync()
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QueryAsync<Uom>("SELECT id, code, name, type FROM uom ORDER BY type, code");
    }
}
