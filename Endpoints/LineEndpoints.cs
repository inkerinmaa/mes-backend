using MyDashboardApi.Database.Repositories;

namespace MyDashboardApi.Endpoints;

public static class LineEndpoints
{
    public static IEndpointRouteBuilder MapLineEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/lines", GetLines).RequireAuthorization().WithName("GetLines");
        return app;
    }

    private static async Task<IResult> GetLines(ILineRepository lines)
        => Results.Ok(await lines.GetLinesAsync());
}
