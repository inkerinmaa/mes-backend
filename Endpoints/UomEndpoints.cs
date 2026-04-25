using MyDashboardApi.Database.Repositories;

namespace MyDashboardApi.Endpoints;

public static class UomEndpoints
{
    public static IEndpointRouteBuilder MapUomEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/uoms", GetUoms).RequireAuthorization().WithName("GetUoms");
        return app;
    }

    private static async Task<IResult> GetUoms(IUomRepository uoms)
    {
        return Results.Ok(await uoms.GetUomsAsync());
    }
}
