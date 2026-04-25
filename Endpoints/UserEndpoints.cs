using MyDashboardApi.Database.Repositories;
using MyDashboardApi.Models;
using Microsoft.Extensions.Logging;

namespace MyDashboardApi.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();
        group.MapPost("/me", SyncUser).WithName("SyncUser");
        group.MapPatch("/me/name", UpdateName).WithName("UpdateUserName");
        group.MapGet("/users", GetUsers).WithName("GetUsers");
        group.MapPatch("/users/{id:int}/role", UpdateRole).WithName("UpdateUserRole");
        return app;
    }

    private static async Task<IResult> SyncUser(IUserRepository users, HttpContext ctx)
    {
        var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
        if (string.IsNullOrEmpty(keycloakId))
            return Results.BadRequest(new { error = "Missing sub claim" });

        var email    = ctx.User.FindFirst("email")?.Value ?? "";
        var username = ctx.User.FindFirst("preferred_username")?.Value ?? "";
        var fullName = ctx.User.FindFirst("name")?.Value
            ?? $"{ctx.User.FindFirst("given_name")?.Value} {ctx.User.FindFirst("family_name")?.Value}".Trim();

        var (id, role, dbFullName) = await users.UpsertUserAsync(keycloakId, email, username, fullName);
        // Return full_name from the DB — user may have edited it via settings, and
        // the ON CONFLICT clause does not overwrite full_name on subsequent logins.
        return Results.Ok(new { id, email, username, fullName = dbFullName, role });
    }

    private static async Task<IResult> UpdateName(
        UpdateNameRequest req, IUserRepository users, HttpContext ctx)
    {
        var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
        if (string.IsNullOrWhiteSpace(req.FullName))
            return Results.BadRequest(new { error = "Name cannot be empty" });

        await users.UpdateUserNameAsync(keycloakId, req.FullName.Trim());
        return Results.Ok(new { fullName = req.FullName.Trim() });
    }

    private static async Task<IResult> GetUsers(IUserRepository users)
    {
        return Results.Ok(await users.GetUsersAsync());
    }

    private static async Task<IResult> UpdateRole(
        int id, UpdateRoleRequest req, IUserRepository users,
        HttpContext ctx, ILogger<Program> logger)
    {
        var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
        var (_, callerRole) = await users.GetUserContextAsync(keycloakId);
        if (callerRole != "admin")
        {
            logger.LogWarning("Unauthorized role change attempt by {KeycloakId}", keycloakId);
            return Results.Forbid();
        }

        if (req.Role != "admin" && req.Role != "viewer")
            return Results.BadRequest(new { error = "Role must be 'admin' or 'viewer'" });

        await users.UpdateUserRoleAsync(id, req.Role);
        return Results.Ok(new { id, role = req.Role });
    }
}
