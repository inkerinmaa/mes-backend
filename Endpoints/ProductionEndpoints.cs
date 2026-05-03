using MyDashboardApi.Database.Repositories;

namespace MyDashboardApi.Endpoints;

public static class ProductionEndpoints
{
    public static void MapProductionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/production").RequireAuthorization();

        group.MapGet("/lines/{lineId:int}/units/{unit}/latest", async (
            int lineId, string unit,
            IProcessRepository repo) =>
        {
            if (lineId < 1) return Results.BadRequest("lineId must be >= 1");
            var data = await repo.GetLatestAsync(lineId, unit);
            return Results.Ok(data);
        });
    }
}
