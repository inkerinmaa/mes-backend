using MyDashboardApi.Database.Repositories;

namespace MyDashboardApi.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports").RequireAuthorization();
        group.MapGet("/pkf",    GetPkf).WithName("GetPkfReport");
        group.MapGet("/energy", GetEnergy).WithName("GetEnergyReport");
        group.MapGet("/waste",  GetWaste).WithName("GetWasteReport");
        return app;
    }

    // GET /api/reports/pkf?lineId=1&startDate=2026-04-01&endDate=2026-04-30
    // GET /api/reports/pkf?orderNumber=ORD-2026-L1-001
    private static async Task<IResult> GetPkf(
        IReportRepository reports,
        int? lineId, string? startDate, string? endDate, string? orderNumber)
    {
        if (!string.IsNullOrEmpty(orderNumber))
            return Results.Ok(await reports.GetPkfByOrderAsync(orderNumber));

        if (lineId is null || string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
            return Results.BadRequest(new { error = "Provide either orderNumber or lineId+startDate+endDate" });

        if (!DateOnly.TryParse(startDate, out _) || !DateOnly.TryParse(endDate, out _))
            return Results.BadRequest(new { error = "Invalid date format (expected YYYY-MM-DD)" });

        return Results.Ok(await reports.GetPkfByPeriodAsync(lineId.Value, startDate, endDate));
    }

    // GET /api/reports/energy?lineId=1&startDate=2026-04-01&endDate=2026-04-30
    // GET /api/reports/energy?orderNumber=ORD-2026-L1-001
    private static async Task<IResult> GetEnergy(
        IReportRepository reports,
        int? lineId, string? startDate, string? endDate, string? orderNumber)
    {
        if (!string.IsNullOrEmpty(orderNumber))
            return Results.Ok(await reports.GetEnergyByOrderAsync(orderNumber));

        if (lineId is null || string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
            return Results.BadRequest(new { error = "Provide either orderNumber or lineId+startDate+endDate" });

        if (!DateOnly.TryParse(startDate, out _) || !DateOnly.TryParse(endDate, out _))
            return Results.BadRequest(new { error = "Invalid date format (expected YYYY-MM-DD)" });

        return Results.Ok(await reports.GetEnergyByPeriodAsync(lineId.Value, startDate, endDate));
    }

    // GET /api/reports/waste?lineId=1&startDate=2026-04-01&endDate=2026-04-30
    // GET /api/reports/waste?orderNumber=ORD-2026-L1-001
    private static async Task<IResult> GetWaste(
        IReportRepository reports,
        int? lineId, string? startDate, string? endDate, string? orderNumber)
    {
        if (!string.IsNullOrEmpty(orderNumber))
            return Results.Ok(await reports.GetWasteByOrderAsync(orderNumber));

        if (lineId is null || string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
            return Results.BadRequest(new { error = "Provide either orderNumber or lineId+startDate+endDate" });

        if (!DateOnly.TryParse(startDate, out _) || !DateOnly.TryParse(endDate, out _))
            return Results.BadRequest(new { error = "Invalid date format (expected YYYY-MM-DD)" });

        return Results.Ok(await reports.GetWasteByPeriodAsync(lineId.Value, startDate, endDate));
    }
}
