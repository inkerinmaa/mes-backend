using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        var authority = builder.Configuration["Authentication:Authority"]!;
        var audience = builder.Configuration["Authentication:Audience"]!;
        var backchannelAuthority = builder.Configuration["Authentication:BackchannelAuthority"];

        // Use backchannel authority for JWKS/metadata fetching (avoids DNS/SSL issues in WSL)
        // but validate the issuer against the public-facing authority URL
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

        // Allow JWT token in query string for SignalR
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

// --- Dashboard Stats ---
app.MapGet("/api/dashboard/stats", () =>
{
    return new
    {
        customers = new { value = Random.Shared.Next(400, 1000), variation = Random.Shared.Next(-15, 26) },
        conversions = new { value = Random.Shared.Next(1000, 2000), variation = Random.Shared.Next(-10, 21) },
        revenue = new { value = Random.Shared.Next(200000, 500000), variation = Random.Shared.Next(-20, 31) },
        orders = new { value = Random.Shared.Next(100, 300), variation = Random.Shared.Next(-5, 16) }
    };
})
.WithName("GetDashboardStats")
.RequireAuthorization();

// --- Dashboard Revenue Chart ---
app.MapGet("/api/dashboard/revenue", (string? period, string? startDate, string? endDate) =>
{
    var start = startDate != null ? DateOnly.Parse(startDate) : DateOnly.FromDateTime(DateTime.Now.AddDays(-14));
    var end = endDate != null ? DateOnly.Parse(endDate) : DateOnly.FromDateTime(DateTime.Now);

    var points = new List<object>();
    var current = start;

    while (current <= end)
    {
        points.Add(new { date = current.ToString("yyyy-MM-dd"), amount = Random.Shared.Next(1000, 10001) });

        current = period switch
        {
            "weekly" => current.AddDays(7),
            "monthly" => current.AddMonths(1),
            _ => current.AddDays(1)
        };
    }

    return points;
})
.WithName("GetDashboardRevenue")
.RequireAuthorization();

// --- Dashboard Recent Sales ---
app.MapGet("/api/dashboard/sales", () =>
{
    var emails = new[] { "james.anderson@example.com", "mia.white@example.com", "william.brown@example.com", "emma.davis@example.com", "ethan.harris@example.com" };
    var statuses = new[] { "paid", "failed", "refunded" };
    var now = DateTime.UtcNow;

    return Enumerable.Range(0, 5).Select(i => new
    {
        id = (4600 - i).ToString(),
        date = now.AddHours(-Random.Shared.Next(0, 48)).ToString("o"),
        status = statuses[Random.Shared.Next(statuses.Length)],
        email = emails[Random.Shared.Next(emails.Length)],
        amount = Random.Shared.Next(100, 1001)
    })
    .OrderByDescending(s => s.date)
    .ToArray();
})
.WithName("GetDashboardSales")
.RequireAuthorization();

// --- Customers ---
app.MapGet("/api/customers", () =>
{
    var firstNames = new[] { "James", "Mia", "William", "Emma", "Ethan", "Olivia", "Liam", "Sophia", "Noah", "Ava", "Lucas", "Isabella", "Mason", "Charlotte", "Logan" };
    var lastNames = new[] { "Anderson", "White", "Brown", "Davis", "Harris", "Miller", "Wilson", "Moore", "Taylor", "Thomas", "Jackson", "Martin", "Lee", "Garcia", "Clark" };
    var locations = new[] { "New York, NY", "Los Angeles, CA", "Chicago, IL", "Houston, TX", "Phoenix, AZ", "Philadelphia, PA", "San Antonio, TX", "San Diego, CA", "Dallas, TX", "Austin, TX" };
    var statuses = new[] { "subscribed", "unsubscribed", "bounced" };

    return Enumerable.Range(1, 30).Select(i =>
    {
        var first = firstNames[Random.Shared.Next(firstNames.Length)];
        var last = lastNames[Random.Shared.Next(lastNames.Length)];
        return new
        {
            id = i,
            name = $"{first} {last}",
            email = $"{first.ToLower()}.{last.ToLower()}@example.com",
            avatar = new { src = $"https://i.pravatar.cc/150?u={i}", alt = $"{first} {last}" },
            status = statuses[Random.Shared.Next(statuses.Length)],
            location = locations[Random.Shared.Next(locations.Length)]
        };
    }).ToArray();
})
.WithName("GetCustomers")
.RequireAuthorization();

// --- Mails ---
app.MapGet("/api/mails", () =>
{
    var senders = new[]
    {
        new { id = 101, name = "Jane Smith", email = "jane.smith@example.com", status = "subscribed", location = "Los Angeles, CA" },
        new { id = 102, name = "Mike Johnson", email = "mike.johnson@example.com", status = "subscribed", location = "Chicago, IL" },
        new { id = 103, name = "Sarah Connor", email = "sarah.connor@example.com", status = "subscribed", location = "Dallas, TX" },
        new { id = 104, name = "Tom Hardy", email = "tom.hardy@example.com", status = "unsubscribed", location = "New York, NY" }
    };

    var subjects = new[] { "Project Update", "Meeting Tomorrow", "Invoice #1234", "Quick Question", "Weekly Report", "New Feature Request", "Bug Report", "Documentation Review" };
    var bodies = new[]
    {
        "Hi team, here's the latest update on the project. We've made significant progress on the frontend implementation and the API endpoints are now fully functional.",
        "Just a reminder that we have a team meeting scheduled for tomorrow at 10 AM. Please come prepared with your weekly status updates.",
        "Please find attached the invoice for this month's services. Let me know if you have any questions about the billing.",
        "I had a quick question about the deployment process. Could you walk me through the steps when you get a chance?",
        "Here's the weekly report summarizing our progress. Key highlights include the completion of the auth module and initial SignalR integration.",
        "I'd like to propose a new feature for the dashboard - real-time notifications for production line alerts. What do you think?",
        "Found a bug in the order processing module. When submitting orders with special characters, the validation fails silently.",
        "Could you review the updated documentation for the API endpoints? I've added examples for all the new dashboard routes."
    };

    var now = DateTime.UtcNow;

    return Enumerable.Range(1, 8).Select(i =>
    {
        var sender = senders[Random.Shared.Next(senders.Length)];
        return new
        {
            id = i,
            unread = i <= 3,
            from = new
            {
                sender.id,
                sender.name,
                sender.email,
                avatar = new { src = $"https://i.pravatar.cc/150?u=sender{sender.id}", alt = sender.name },
                sender.status,
                sender.location
            },
            subject = subjects[i - 1],
            body = bodies[i - 1],
            date = now.AddHours(-Random.Shared.Next(1, 72)).ToString("o")
        };
    })
    .OrderByDescending(m => m.date)
    .ToArray();
})
.WithName("GetMails")
.RequireAuthorization();

// --- Notifications ---
app.MapGet("/api/notifications", () =>
{
    var senders = new[]
    {
        new { id = 201, name = "Alex Turner", email = "alex.turner@example.com", avatar = new { src = "https://i.pravatar.cc/150?u=alex", alt = "Alex Turner" }, status = "subscribed", location = "Seattle, WA" },
        new { id = 202, name = "Rachel Green", email = "rachel.green@example.com", avatar = new { src = "https://i.pravatar.cc/150?u=rachel", alt = "Rachel Green" }, status = "subscribed", location = "Boston, MA" },
        new { id = 203, name = "David Kim", email = "david.kim@example.com", avatar = new { src = "https://i.pravatar.cc/150?u=david", alt = "David Kim" }, status = "subscribed", location = "Portland, OR" }
    };

    var bodies = new[]
    {
        "Mentioned you in a comment on the production report.",
        "Assigned you a new task: Review Q4 metrics.",
        "Updated the shipping schedule for next week.",
        "Completed the maintenance checklist for Line A.",
        "Requested approval for overtime on Saturday."
    };

    var now = DateTime.UtcNow;

    return Enumerable.Range(1, 5).Select(i => new
    {
        id = i,
        unread = i <= 2,
        sender = senders[Random.Shared.Next(senders.Length)],
        body = bodies[i - 1],
        date = now.AddHours(-Random.Shared.Next(1, 48)).ToString("o")
    })
    .OrderByDescending(n => n.date)
    .ToArray();
})
.WithName("GetNotifications")
.RequireAuthorization();

// --- Members ---
app.MapGet("/api/members", () =>
{
    var members = new[]
    {
        new { name = "Benjamin Canac", username = "benjamincanac", role = "owner", avatar = new { src = "https://i.pravatar.cc/150?u=ben", alt = "Benjamin Canac" } },
        new { name = "Romain Hamel", username = "romhml", role = "member", avatar = new { src = "https://i.pravatar.cc/150?u=romain", alt = "Romain Hamel" } },
        new { name = "Sylvain Marroufin", username = "smarroufin", role = "member", avatar = new { src = "https://i.pravatar.cc/150?u=sylvain", alt = "Sylvain Marroufin" } },
        new { name = "Sébastien Chopin", username = "atinux", role = "member", avatar = new { src = "https://i.pravatar.cc/150?u=seb", alt = "Sébastien Chopin" } },
        new { name = "Daniel Roe", username = "danielroe", role = "member", avatar = new { src = "https://i.pravatar.cc/150?u=daniel", alt = "Daniel Roe" } }
    };

    return members;
})
.WithName("GetMembers")
.RequireAuthorization();

// --- SignalR Hub ---
app.MapHub<DashboardHub>("/hubs/dashboard").RequireAuthorization();

app.Run();

// === SignalR Hub ===
public class DashboardHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}

// === Background service that pushes Orders updates every 5 seconds ===
public class OrdersBackgroundService : BackgroundService
{
    private readonly IHubContext<DashboardHub> _hubContext;

    public OrdersBackgroundService(IHubContext<DashboardHub> hubContext)
    {
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var currentOrders = Random.Shared.Next(100, 300);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Simulate order count changing by -3 to +5
            currentOrders += Random.Shared.Next(-3, 6);
            currentOrders = Math.Clamp(currentOrders, 50, 500);

            var variation = Random.Shared.Next(-5, 16);

            await _hubContext.Clients.All.SendAsync("OrdersUpdated", new
            {
                value = currentOrders,
                variation
            }, CancellationToken.None);
        }
    }
}
