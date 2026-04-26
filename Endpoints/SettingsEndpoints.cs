using Microsoft.AspNetCore.Mvc;
using MyDashboardApi.Database.Repositories;

namespace MyDashboardApi.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings").RequireAuthorization();

        group.MapGet("/", async (ISettingsRepository settings) =>
            Results.Ok(await settings.GetAllAsync()));

        group.MapPatch("/{key}", async (
            string key,
            [FromBody] UpdateSettingRequest req,
            ISettingsRepository settings,
            IUserRepository users,
            ILogRepository logs,
            HttpContext ctx) =>
        {
            var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
            var (userId, _) = await users.GetUserContextAsync(keycloakId);

            var updated = await settings.SetAsync(key, req.Value, userId);
            if (updated is null)
                return Results.NotFound(new { error = $"Setting '{key}' not found" });

            await logs.WriteAsync("APP", "INFO",
                $"Setting '{key}' changed: '{updated.PreviousValue ?? "—"}' → '{updated.Value}'");

            return Results.Ok(updated);
        });
    }
}

public record UpdateSettingRequest(string Value);
