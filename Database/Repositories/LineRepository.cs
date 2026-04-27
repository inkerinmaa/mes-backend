using Dapper;
using Npgsql;
using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public class LineRepository(NpgsqlDataSource dataSource) : ILineRepository
{
    public async Task<IEnumerable<ProductionLine>> GetLinesAsync()
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QueryAsync<ProductionLine>(
            "SELECT id, name FROM production_lines ORDER BY id");
    }
}
