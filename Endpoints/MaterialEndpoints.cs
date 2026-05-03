using MyDashboardApi.Database.Repositories;

namespace MyDashboardApi.Endpoints;

public static class MaterialEndpoints
{
    public static IEndpointRouteBuilder MapMaterialEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/materials", GetMaterials).RequireAuthorization().WithName("GetMaterials");
        return app;
    }

    private static async Task<IResult> GetMaterials(IMaterialRepository materials)
        => Results.Ok(await materials.GetAllAsync());
}
