using MyDashboardApi.Endpoints;
using MyDashboardApi.Hubs;
using MyDashboardApi.Services;

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapDashboardEndpoints();
app.MapCustomerEndpoints();
app.MapMailEndpoints();
app.MapNotificationEndpoints();
app.MapMemberEndpoints();
app.MapHub<DashboardHub>("/hubs/dashboard").RequireAuthorization();

app.Run();
