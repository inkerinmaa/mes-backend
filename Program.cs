using Dapper;
using MyDashboardApi.Database.Repositories;
using MyDashboardApi.Endpoints;
using MyDashboardApi.Hubs;
using MyDashboardApi.Services;
using MyDashboardApi.Models;

// Map snake_case SQL column names to PascalCase C# properties/constructors globally
DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        var authority = builder.Configuration["Authentication:Authority"]!;
        var audience = builder.Configuration["Authentication:Audience"]!;
        var backchannelAuthority = builder.Configuration["Authentication:BackchannelAuthority"];

        if (!string.IsNullOrEmpty(backchannelAuthority))
        {
            options.MetadataAddress = $"{backchannelAuthority}/.well-known/openid-configuration";
            options.RequireHttpsMetadata = false;
        }
        else
        {
            options.Authority = authority;
            options.RequireHttpsMetadata = true;
        }

        options.Audience = audience;

        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authority,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true
        };

        options.MapInboundClaims = false;

        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddHostedService<OrdersBackgroundService>();
builder.Services.AddHostedService<ProcessDataService>();

var connStr = builder.Configuration.GetConnectionString("Postgres")!;
var dataSource = Npgsql.NpgsqlDataSource.Create(connStr);
builder.Services.AddSingleton(dataSource);
builder.Services.AddSingleton<IOrderRepository, OrderRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<ISkuRepository, SkuRepository>();
builder.Services.AddSingleton<IUomRepository, UomRepository>();
builder.Services.AddSingleton<ILogRepository, LogRepository>();
builder.Services.AddSingleton<IMachineStateRepository, MachineStateRepository>();
builder.Logging.AddProvider(new DbLoggerProvider(Npgsql.NpgsqlDataSource.Create(connStr)));

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("MES API starting up...");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

// Auto-provision any authenticated user into the users table on first request.
// Uses an in-memory set so DB is only hit once per user per process lifetime.
var provisionedUsers = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();
app.Use(async (ctx, next) =>
{
    if (ctx.User.Identity?.IsAuthenticated == true)
    {
        var keycloakId = ctx.User.FindFirst("sub")?.Value;
        if (keycloakId != null && provisionedUsers.TryAdd(keycloakId, true))
        {
            var users = ctx.RequestServices.GetRequiredService<IUserRepository>();
            var email    = ctx.User.FindFirst("email")?.Value ?? "";
            var username = ctx.User.FindFirst("preferred_username")?.Value ?? "";
            var fullName = ctx.User.FindFirst("name")?.Value
                ?? $"{ctx.User.FindFirst("given_name")?.Value} {ctx.User.FindFirst("family_name")?.Value}".Trim();
            await users.UpsertUserAsync(keycloakId, email, username, fullName);
        }
    }
    await next();
});

app.MapDashboardEndpoints();
app.MapMachineStateEndpoints();
app.MapOrderEndpoints();
app.MapSkuEndpoints();
app.MapUomEndpoints();
app.MapUserEndpoints();
app.MapNotificationEndpoints();
app.MapMemberEndpoints();
app.MapHub<DashboardHub>("/hubs/dashboard").RequireAuthorization();

app.Run();
