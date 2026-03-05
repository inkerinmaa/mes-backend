# Project: MyDashboardApi

## Stack
- .NET 10 minimal API
- JWT Bearer authentication via Keycloak (OIDC)
- SignalR for real-time WebSocket events
- Npgsql (PostgreSQL) + ClickHouse.Client (future)
- Runs on WSL2, listens on http://localhost:5000

## Commands
- Run dev: `dotnet run`
- Build: `dotnet build`
- Restore packages: `dotnet restore`
- HTTP smoke tests: open `MyDashboardApi.http` in VS Code REST Client

## Architecture
- `Program.cs` — DI setup, middleware pipeline, endpoint mapping
- `Endpoints/` — minimal API route handlers (DashboardEndpoints, CustomerEndpoints, etc.)
- `Models/` — records/classes for DTOs (StatMetric, Order, Customer, etc.)
- `Hubs/DashboardHub.cs` — SignalR hub mounted at `/hubs/dashboard`
- `Services/` — background services that push data via SignalR
  - `ProcessDataService.cs` — simulates OPC UA telemetry every 3 s
  - `OrdersBackgroundService.cs` — simulates business events every 5 s
- `appsettings.json` — connection strings, OPC UA URL, auth config
- `Properties/launchSettings.json` — launch profile (port 5000, HTTP)

## Key endpoints (all require JWT)
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/dashboard/stats` | KPI metrics |
| GET | `/api/dashboard/efficiency` | Production efficiency over date range |
| GET | `/api/dashboard/events` | Recent production events |
| GET | `/api/dashboard/states` | Machine state timeline |
| GET | `/api/dashboard/current-orders` | Orders in queue |

## SignalR events (`/hubs/dashboard`)
- `OrdersUpdated` — every 5 s, `{ value, variation }`
- `ProcessDataUpdated` — every 3 s, `{ timestamp, temperature, pressure, cycleTime, machineState }`
- `StatsUpdated` — every 3 s, `{ totalTonnes, lineUptime, wastePercentage, … }`

## Authentication config (`appsettings.json`)
```json
{
  "Authentication": {
    "Authority": "https://keycloak.test.local/realms/mes-realm",
    "BackchannelAuthority": "http://localhost:8080/realms/mes-realm",
    "Audience": "mes-frontend"
  }
}
```
- **Authority** — must match the `iss` claim in the JWT
- **BackchannelAuthority** — used internally to fetch JWKS/OIDC metadata over plain HTTP (avoids self-signed cert issues)
- **Audience** — must match the `aud` claim (requires audience mapper in Keycloak)

## Rules
- All route handlers live in `Endpoints/`, not in `Program.cs`
- Always keep `BackchannelAuthority` pointing to `http://localhost:8080` in dev so .NET can reach Keycloak without SSL
- Never commit real connection string passwords to source control
- Background services must be registered with `AddHostedService<T>()` in `Program.cs`
