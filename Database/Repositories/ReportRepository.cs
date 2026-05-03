using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using MyDashboardApi.Models;
using Npgsql;

namespace MyDashboardApi.Database.Repositories;

public class ReportRepository(IHttpClientFactory httpClientFactory, NpgsqlDataSource pg) : IReportRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly Regex SafeOrderNumber = new(@"^[A-Za-z0-9\-]+$", RegexOptions.Compiled);

    // ── ClickHouse helpers ────────────────────────────────────────────────────

    private async Task<List<T>> QueryChAsync<T>(string sql)
    {
        var client = httpClientFactory.CreateClient("clickhouse");
        var response = await client.PostAsync("/", new StringContent(sql, Encoding.UTF8, "text/plain"));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var result = new List<T>();
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var item = JsonSerializer.Deserialize<T>(line, JsonOpts);
            if (item != null) result.Add(item);
        }
        return result;
    }

    private static string PkfAggSql(string whereClause) => $"""
        SELECT
            order_number        AS orderNumber,
            formatDateTime(MIN(ts), '%Y-%m-%dT%H:%i:%S') AS startTs,
            formatDateTime(MAX(ts), '%Y-%m-%dT%H:%i:%S') AS endTs,
            round(dateDiff('second', MIN(ts), MAX(ts)) / 3600.0, 2) AS durationH,
            round(SUM(basalt_kg) / 1000, 3)  AS basaltT,
            round(SUM(binder_kg), 1)          AS binderKg,
            round(SUM(wool_kg)   / 1000, 3)  AS woolT,
            round(SUM(waste_kg), 1)           AS wasteKg,
            round(AVG(efficiency), 1)         AS avgEfficiency
        FROM historian.production_metrics
        WHERE {whereClause}
        GROUP BY order_number
        ORDER BY MIN(ts)
        FORMAT JSONEachRow
        """;

    private static string WasteAggSql(string whereClause) => $"""
        SELECT
            order_number        AS orderNumber,
            formatDateTime(MIN(ts), '%Y-%m-%dT%H:%i:%S') AS startTs,
            formatDateTime(MAX(ts), '%Y-%m-%dT%H:%i:%S') AS endTs,
            round(dateDiff('second', MIN(ts), MAX(ts)) / 3600.0, 2) AS durationH,
            round(SUM(trimming_kg), 1) AS trimmingKg,
            round(SUM(startup_kg),  1) AS startupKg,
            round(SUM(rejected_kg), 1) AS rejectedKg,
            round(SUM(total_kg),    1) AS totalKg,
            round(AVG(waste_pct),   1) AS wastePct
        FROM historian.waste_metrics
        WHERE {whereClause}
        GROUP BY order_number
        ORDER BY MIN(ts)
        FORMAT JSONEachRow
        """;

    private static string EnergyAggSql(string whereClause) => $"""
        SELECT
            order_number        AS orderNumber,
            formatDateTime(MIN(ts), '%Y-%m-%dT%H:%i:%S') AS startTs,
            formatDateTime(MAX(ts), '%Y-%m-%dT%H:%i:%S') AS endTs,
            round(dateDiff('second', MIN(ts), MAX(ts)) / 3600.0, 2) AS durationH,
            round(SUM(gas_m3),   2) AS totalGasM3,
            round(SUM(elec_kwh), 1) AS totalElecKwh,
            round(SUM(water_m3), 3) AS totalWaterM3
        FROM historian.energy_metrics
        WHERE {whereClause}
        GROUP BY order_number
        ORDER BY MIN(ts)
        FORMAT JSONEachRow
        """;

    // ── PostgreSQL enrichment ─────────────────────────────────────────────────

    private async Task<Dictionary<string, (string? SkuCode, string? SkuName)>> GetSkuMapAsync(IEnumerable<string> orderNumbers)
    {
        var nums = orderNumbers.ToArray();
        if (nums.Length == 0) return new();
        await using var conn = await pg.OpenConnectionAsync();
        var rows = await conn.QueryAsync<(string OrderNumber, string? SkuCode, string? SkuName)>(
            "SELECT o.order_number, s.code AS sku_code, s.name AS sku_name " +
            "FROM orders o JOIN skus s ON s.id = o.sku_id WHERE o.order_number = ANY(@nums)",
            new { nums });
        return rows.ToDictionary(r => r.OrderNumber, r => (r.SkuCode, r.SkuName));
    }

    // ── Public methods ────────────────────────────────────────────────────────

    public async Task<List<PkfReportRow>> GetPkfByPeriodAsync(int lineId, string startDate, string endDate)
    {
        var where = $"line_id = {lineId} AND ts >= toDateTime('{startDate} 00:00:00') AND ts < addDays(toDateTime('{endDate} 00:00:00'), 1)";
        var rows = await QueryChAsync<PkfChRow>(PkfAggSql(where));
        var skus  = await GetSkuMapAsync(rows.Select(r => r.OrderNumber));
        return rows.Select(r => {
            skus.TryGetValue(r.OrderNumber, out var s);
            return new PkfReportRow(r.OrderNumber, s.SkuCode, s.SkuName, r.StartTs, r.EndTs, r.DurationH, r.BasaltT, r.BinderKg, r.WoolT, r.WasteKg, r.AvgEfficiency);
        }).ToList();
    }

    public async Task<List<PkfReportRow>> GetPkfByOrderAsync(string orderNumber)
    {
        if (!SafeOrderNumber.IsMatch(orderNumber)) return [];
        var where = $"order_number = '{orderNumber}'";
        var rows = await QueryChAsync<PkfChRow>(PkfAggSql(where));
        var skus  = await GetSkuMapAsync(rows.Select(r => r.OrderNumber));
        return rows.Select(r => {
            skus.TryGetValue(r.OrderNumber, out var s);
            return new PkfReportRow(r.OrderNumber, s.SkuCode, s.SkuName, r.StartTs, r.EndTs, r.DurationH, r.BasaltT, r.BinderKg, r.WoolT, r.WasteKg, r.AvgEfficiency);
        }).ToList();
    }

    public async Task<List<EnergyReportRow>> GetEnergyByPeriodAsync(int lineId, string startDate, string endDate)
    {
        var where = $"line_id = {lineId} AND ts >= toDateTime('{startDate} 00:00:00') AND ts < addDays(toDateTime('{endDate} 00:00:00'), 1)";
        var rows = await QueryChAsync<EnergyChRow>(EnergyAggSql(where));
        var skus  = await GetSkuMapAsync(rows.Select(r => r.OrderNumber));
        return rows.Select(r => {
            skus.TryGetValue(r.OrderNumber, out var s);
            return new EnergyReportRow(r.OrderNumber, s.SkuCode, s.SkuName, r.StartTs, r.EndTs, r.DurationH, r.TotalGasM3, r.TotalElecKwh, r.TotalWaterM3);
        }).ToList();
    }

    public async Task<List<EnergyReportRow>> GetEnergyByOrderAsync(string orderNumber)
    {
        if (!SafeOrderNumber.IsMatch(orderNumber)) return [];
        var where = $"order_number = '{orderNumber}'";
        var rows = await QueryChAsync<EnergyChRow>(EnergyAggSql(where));
        var skus  = await GetSkuMapAsync(rows.Select(r => r.OrderNumber));
        return rows.Select(r => {
            skus.TryGetValue(r.OrderNumber, out var s);
            return new EnergyReportRow(r.OrderNumber, s.SkuCode, s.SkuName, r.StartTs, r.EndTs, r.DurationH, r.TotalGasM3, r.TotalElecKwh, r.TotalWaterM3);
        }).ToList();
    }

    public async Task<List<WasteReportRow>> GetWasteByPeriodAsync(int lineId, string startDate, string endDate)
    {
        var where = $"line_id = {lineId} AND ts >= toDateTime('{startDate} 00:00:00') AND ts < addDays(toDateTime('{endDate} 00:00:00'), 1)";
        var rows = await QueryChAsync<WasteChRow>(WasteAggSql(where));
        var skus  = await GetSkuMapAsync(rows.Select(r => r.OrderNumber));
        return rows.Select(r => {
            skus.TryGetValue(r.OrderNumber, out var s);
            return new WasteReportRow(r.OrderNumber, s.SkuCode, s.SkuName, r.StartTs, r.EndTs, r.DurationH, r.TrimmingKg, r.StartupKg, r.RejectedKg, r.TotalKg, r.WastePct);
        }).ToList();
    }

    public async Task<List<WasteReportRow>> GetWasteByOrderAsync(string orderNumber)
    {
        if (!SafeOrderNumber.IsMatch(orderNumber)) return [];
        var where = $"order_number = '{orderNumber}'";
        var rows = await QueryChAsync<WasteChRow>(WasteAggSql(where));
        var skus  = await GetSkuMapAsync(rows.Select(r => r.OrderNumber));
        return rows.Select(r => {
            skus.TryGetValue(r.OrderNumber, out var s);
            return new WasteReportRow(r.OrderNumber, s.SkuCode, s.SkuName, r.StartTs, r.EndTs, r.DurationH, r.TrimmingKg, r.StartupKg, r.RejectedKg, r.TotalKg, r.WastePct);
        }).ToList();
    }
}
