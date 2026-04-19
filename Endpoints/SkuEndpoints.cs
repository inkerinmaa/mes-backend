using MyDashboardApi.Database.Repositories;

namespace MyDashboardApi.Endpoints;

public static class SkuEndpoints
{
    public static IEndpointRouteBuilder MapSkuEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();
        group.MapGet("/skus", async (ISkuRepository skus) => Results.Ok(await skus.GetSkusAsync()))
             .WithName("GetSkus");
        return app;
    }
}
