# MyDashboardApi — Agent Reference

## Project overview
Manufacturing Execution System (MES) backend API. Serves dashboard KPIs, production events, and machine telemetry. Pushes real-time updates over SignalR. Authenticates all requests via Keycloak-issued JWTs.

## How to run
```bash
cd ~/projects/MyDashboardApi
dotnet restore
dotnet run          # http://localhost:5000
```

## How to test endpoints
All endpoints require a valid JWT. Obtain one from Keycloak first:
```bash
# Get token (testuser / testpassword)
TOKEN=$(curl -s -X POST \
  http://localhost:8080/realms/mes-realm/protocol/openid-connect/token \
  -d "client_id=mes-frontend&grant_type=password&username=testuser&password=testpassword" \
  | jq -r .access_token)

curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/dashboard/stats
```

## Key files
| File | Purpose |
|------|---------|
| `Program.cs` | App bootstrap, DI, middleware, route mapping |
| `appsettings.json` | DB connections, auth config, OPC UA URL |
| `Endpoints/*.cs` | Minimal API route handlers |
| `Hubs/DashboardHub.cs` | SignalR hub |
| `Services/ProcessDataService.cs` | Telemetry simulation background service |
| `Services/OrdersBackgroundService.cs` | Orders simulation background service |

## Conventions
- Endpoints are grouped in static `Map*` extension methods inside `Endpoints/`
- DTOs are records in `Models/`
- Background services implement `BackgroundService` and inject `IHubContext<DashboardHub>`
- Authentication is configured once in `Program.cs` with `.AddJwtBearer()`

## Do not
- Add routes directly inside `Program.cs` — use `Endpoints/`
- Disable JWT validation for convenience — always test with a real token
- Change `Authority` without also updating `BackchannelAuthority` (they serve different purposes)
- Skip `dotnet restore` after pulling — NuGet packages may be out of sync
