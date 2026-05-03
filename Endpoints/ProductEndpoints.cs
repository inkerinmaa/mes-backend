using MyDashboardApi.Database.Repositories;
using MyDashboardApi.Models;

namespace MyDashboardApi.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products").RequireAuthorization();
        group.MapGet("/",      GetProducts).WithName("GetProducts");
        group.MapGet("/{id:int}", GetProductDetail).WithName("GetProductDetail");
        group.MapPatch("/{id:int}", UpdateProduct).WithName("UpdateProduct");
        return app;
    }

    private static async Task<IResult> GetProducts(IProductRepository products)
        => Results.Ok(await products.GetProductsAsync());

    private static async Task<IResult> GetProductDetail(int id, IProductRepository products)
    {
        var detail = await products.GetProductDetailAsync(id);
        return detail is null ? Results.NotFound(new { error = "Product not found" }) : Results.Ok(detail);
    }

    private static async Task<IResult> UpdateProduct(
        int id, UpdateProductRequest req, IProductRepository products,
        IUserRepository users, HttpContext ctx)
    {
        var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
        var (userId, role) = await users.GetUserContextAsync(keycloakId);
        if (role != "admin") return Results.Forbid();

        var updated = await products.UpdateProductAsync(id, req, userId ?? 0);
        return updated ? Results.Ok(new { id }) : Results.NotFound(new { error = "Product not found" });
    }
}
