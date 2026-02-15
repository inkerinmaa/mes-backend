using MyDashboardApi.Models;

namespace MyDashboardApi.Endpoints;

public static class MemberEndpoints
{
    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        group.MapGet("/members", GetMembers).WithName("GetMembers");

        return app;
    }

    private static Member[] GetMembers()
    {
        return
        [
            new Member("Benjamin Canac", "benjamincanac", "owner", new Avatar("https://i.pravatar.cc/150?u=ben", "Benjamin Canac")),
            new Member("Romain Hamel", "romhml", "member", new Avatar("https://i.pravatar.cc/150?u=romain", "Romain Hamel")),
            new Member("Sylvain Marroufin", "smarroufin", "member", new Avatar("https://i.pravatar.cc/150?u=sylvain", "Sylvain Marroufin")),
            new Member("Sebastien Chopin", "atinux", "member", new Avatar("https://i.pravatar.cc/150?u=seb", "Sebastien Chopin")),
            new Member("Daniel Roe", "danielroe", "member", new Avatar("https://i.pravatar.cc/150?u=daniel", "Daniel Roe"))
        ];
    }
}
