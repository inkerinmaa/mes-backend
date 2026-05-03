using System.Text;
using System.Text.Json;

namespace MyDashboardApi.Database.Repositories;

public class ProcessRepository(IHttpClientFactory httpClientFactory) : IProcessRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly HashSet<string> ValidUnits =
        ["curing", "acon", "binder", "main", "package"];

    public async Task<IEnumerable<ProcessParam>> GetLatestAsync(int lineId, string unit)
    {
        if (!ValidUnits.Contains(unit)) return [];

        var sql = $"""
            SELECT param, value, last_ts AS ts
            FROM (
                SELECT param, argMax(value, ts) AS value, max(ts) AS last_ts
                FROM historian.process_snapshots
                WHERE line_id = {lineId} AND unit = '{unit}'
                GROUP BY param
            )
            ORDER BY param
            FORMAT JSONEachRow
            """;

        var client = httpClientFactory.CreateClient("clickhouse");
        var response = await client.PostAsync("/", new StringContent(sql, Encoding.UTF8, "text/plain"));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        var result = new List<ProcessParam>();
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var item = JsonSerializer.Deserialize<ProcessParam>(line, JsonOpts);
            if (item != null) result.Add(item);
        }
        return result;
    }
}
