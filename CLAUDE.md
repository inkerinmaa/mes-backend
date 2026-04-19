# Project: MyDashboardApi

## Stack
- .NET 10 minimal API
- JWT Bearer authentication via Keycloak (OIDC)
- SignalR for real-time WebSocket events
- Npgsql 9 + Dapper — SQL against PostgreSQL via repository pattern
- Runs on WSL2, listens on `http://localhost:5000`

## Commands
- Run dev: `dotnet run`
- Build: `dotnet build`
- Restore packages: `dotnet restore`
- HTTP smoke tests: open `MyDashboardApi.http` in VS Code REST Client

## Architecture
- `Program.cs` — DI setup, middleware pipeline, endpoint mapping
- `Endpoints/` — minimal API route handlers (one file per domain)
- `Models/` — DTOs and request types
- `Database/Repositories/` — Dapper-based repository implementations
  - `IOrderRepository` / `OrderRepository` — order CRUD
  - `IUserRepository` / `UserRepository` — user upsert, role management
  - `ISkuRepository` / `SkuRepository` — SKU lookup
- `Hubs/DashboardHub.cs` — SignalR hub at `/hubs/dashboard`
- `Services/` — background services pushing data via SignalR
  - `ProcessDataService.cs` — simulates OPC UA telemetry every 3 s
  - `OrdersBackgroundService.cs` — simulates business events every 5 s
- `appsettings.json` — connection strings, OPC UA URL, auth config

## Database schema
Schema lives in `~/projects/dwh/init.sql` — apply once after `docker compose up`:
```bash
psql -h localhost -U nik -d mydb -f ~/projects/dwh/init.sql
```
The API no longer initializes or migrates the schema on startup.

## MES Data Model
Tables in PostgreSQL (auto-created on startup by `MesDb.InitializeAsync`):

| Table | Key columns |
|-------|-------------|
| `users` | `keycloak_id` (JWT sub), `email`, `username`, `full_name`, `last_login` |
| `skus` | `code` (e.g. SKU-A100), `name`, `unit` |
| `production_lines` | `id` (1–3), `name`, `status` |
| `materials` | `code`, `name`, `unit`, `stock_quantity` |
| `orders` | `order_number`, `sku_id`, `production_line_id`, `quantity_packages`, `priority`, `status`, `due_date`, `created_by_id` |

## Endpoints (all require JWT)

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/dashboard/stats` | KPI metrics (mock) |
| GET | `/api/dashboard/efficiency` | Production efficiency over date range (mock) |
| GET | `/api/dashboard/events` | Recent production events (mock) |
| GET | `/api/dashboard/states` | Machine state timeline (mock) |
| GET | `/api/dashboard/current-orders` | Orders in queue snapshot (mock) |
| GET | `/api/orders` | All active orders from PostgreSQL |
| POST | `/api/orders` | Create a new work order |
| GET | `/api/skus` | List all SKUs from PostgreSQL |
| POST | `/api/me` | Upsert current user from JWT claims |
| GET | `/api/members` | Team members (mock) |
| GET | `/api/notifications` | Alerts (mock) |

### POST /api/orders body
```json
{
  "orderNumber": "WO-2024-001",
  "skuCode": "SKU-A100",
  "lineId": 1,
  "quantityPackages": 500,
  "priority": "Medium",
  "dueDate": "2024-12-31"
}
```

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
- **BackchannelAuthority** — used internally to fetch JWKS/OIDC metadata over plain HTTP (avoids self-signed cert issues in dev)
- **Audience** — must match the `aud` claim (requires audience mapper in Keycloak)

## Rules
- All route handlers live in `Endpoints/`, not in `Program.cs`
- All DB access goes through a repository interface — never inline SQL in endpoints
- Repositories are registered as singletons; `NpgsqlDataSource` is the shared connection pool
- `DefaultTypeMap.MatchNamesWithUnderscores = true` is set globally — snake_case columns map to PascalCase properties automatically
- Always keep `BackchannelAuthority` pointing to `http://localhost:8080` in dev
- Background services must be registered with `AddHostedService<T>()` in `Program.cs`
- Never commit real connection string passwords to source control
