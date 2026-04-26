# Project: mes-backend

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
- HTTP smoke tests: open `MyDashboardApi.http` in VS Code REST Client (file kept as-is; filename doesn't affect functionality)

## Architecture
- `Program.cs` — DI setup, middleware pipeline, endpoint mapping
- `Endpoints/` — minimal API route handlers (one file per domain)
- `Models/` — DTOs and request types
- `Database/Repositories/` — Dapper-based repository implementations
  - `IOrderRepository` / `OrderRepository` — order CRUD + cage scanning
  - `IUserRepository` / `UserRepository` — user upsert, role management
  - `ISkuRepository` / `SkuRepository` — SKU lookup
  - `IUomRepository` / `UomRepository` — unit-of-measure lookup
  - `ILogRepository` / `LogRepository` — structured app event log (read + write)
  - `IMachineStateRepository` / `MachineStateRepository` — production line state timeline
- `Hubs/DashboardHub.cs` — SignalR hub at `/hubs/dashboard`
- `Services/` — background services and custom logging
  - `ProcessDataService.cs` — simulates OPC UA telemetry every 3 s; also randomly transitions one production line to a new state every ~30 s, writes it to `machine_states`, and broadcasts `MachineStateUpdated { lineId }` via SignalR. In production this service would be replaced by a real OPC UA subscriber.
  - `OrdersBackgroundService.cs` — simulates business events every 5 s
  - `DbLoggerProvider.cs` / `DbLogger` — custom `ILoggerProvider` that writes `MyDashboardApi.*` log lines to the `logs` table
- `appsettings.json` — connection strings, OPC UA URL, auth config

## Database schema
Schema lives in `~/projects/dwh/init.sql` — apply once after `docker compose up`:
```bash
docker exec -i postgres-db psql -U nik -d mydb < ~/projects/dwh/init.sql
```
The API does not initialize or migrate the schema on startup.

## Data Model

| Table | Key columns | Notes |
|-------|-------------|-------|
| `users` | `keycloak_id`, `email`, `username`, `full_name`, `role`, `last_login` | Upserted on every login via JWT sub |
| `uom` | `code` (kg/t/pcs/m/m2/m3/L/g), `name`, `type` | Unit of measure reference |
| `skus` | `code`, `name`, `description`, `unit` | Product SKU catalogue |
| `production_lines` | `id` (1–3), `name`, `status` | Fixed 3 lines |
| `materials` | `code`, `name`, `unit`, `stock_quantity` | Raw material inventory |
| `orders` | `order_number`, `sku_id`, `production_line_id`, `volume`, `uom_id`, `status`, `priority`, `due_date`, `start_at`, `finish_at`, `comment`, `cage`, `cage_size`, `created_by_id`, `created_at`, `updated_at` | Status: `created`→`running`→`paused`→`running`→`completed`/`cancelled`. `start_at`/`finish_at` are planned times set at creation. `cage=true` enables cage tracking. |
| `cages` | `order_number`, `cage_guid`, `cage_size`, `packages`, `scanned_at`, `scanned_by_id` | One row per QR scan; `cage_size` copied from order at scan time; only `packages` is editable after |
| `machine_states` | `production_line_id`, `state` (running/warning/stopped), `ts` | Append-only event log: one row per state change. Duration is computed dynamically as `LEAD(ts) OVER (...) - ts`; the active segment uses `NOW() - ts`. |
| `logs` | `type` (USER/PROCESS/APP/EQUIPMENT/INTEGRATION), `message`, `level` (DEBUG/INFO/WARNING/ERROR/CRITICAL), `ts` | Structured event log; written automatically by `DbLoggerProvider` for all `MyDashboardApi.*` log lines ≥ INFO |

## Endpoints (all require JWT)

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/dashboard/stats?lineId=` | KPI cards (mock, line-scoped) |
| GET | `/api/dashboard/efficiency?period=&startDate=&endDate=&lineId=` | Line efficiency chart (mock) |
| GET | `/api/dashboard/events?type=&level=&limit=` | Last N log entries from `logs` table |
| GET | `/api/dashboard/states?lineId=` | Machine state timeline from `machine_states` |
| GET | `/api/dashboard/current-orders` | Current order queue snapshot (mock) |
| GET | `/api/logs?type=&level=&limit=` | Filtered log entries from `logs` table |
| GET | `/api/orders` | All non-cancelled orders (with produced_packages) |
| POST | `/api/orders` | Create a new work order |
| DELETE | `/api/orders/{id}` | Cancel an order (admin only; any non-terminal state) |
| PATCH | `/api/orders/{id}/status` | Transition order state `{ action: "start" \| "stop" }` — see state machine below |
| GET | `/api/orders/{id}` | Order detail with cage list |
| POST | `/api/orders/{id}/cages` | Scan cage QR code (`{ qrData: "{order_number}|{uuid}" }`) |
| PATCH | `/api/orders/{id}/cages/{cageId}/packages` | Update packages count for a cage |
| DELETE | `/api/orders/{id}/cages/{cageId}` | Remove a scanned cage |
| PATCH | `/api/orders/{id}/comment` | Update order comment |
| GET | `/api/skus` | All SKUs |
| GET | `/api/uoms` | All units of measure |
| POST | `/api/me` | Upsert current user from JWT claims |
| PATCH | `/api/me/name` | Update display name |
| GET | `/api/members` | Team members (mock) |
| GET | `/api/notifications` | Alerts (mock) |
| POST | `/api/machine-states` | Insert a machine state event `{ lineId, state }` and broadcast `MachineStateUpdated` via SignalR |

## SignalR events (`/hubs/dashboard`)
- `OrdersUpdated` — every 5 s, `{ value, variation }`
- `ProcessDataUpdated` — every 3 s, `{ timestamp, temperature, pressure, cycleTime, machineState }`
- `StatsUpdated` — every 3 s, `{ totalTonnes, lineUptime, wastePercentage, … }`
- `MachineStateUpdated` — on every new `machine_states` row, `{ lineId }`. Sent by `ProcessDataService` (simulation, ~30 s) and by `POST /api/machine-states` (real machine events). Frontend re-fetches the timeline only for the affected line.

## Key implementation details

### Order state machine

```
created ──[Start]──► running ──[Stop]──► paused
   │                    │                  │
   └──[Cancel]──►  cancelled ◄──[Cancel]───┘
                                running ──[Cancel]──► cancelled
completed  (terminal — no transitions out)
cancelled  (terminal — no transitions out)
```

- `start` action: `created` or `paused` → `running`
- `stop` action: `running` → `paused`
- Cancel (`DELETE /api/orders/{id}`): any state except `completed`/`cancelled`
- `start_at` / `finish_at` are **planned** times set at order creation by the planning department; they drive the queue sequence but are not updated by state transitions.
- Sequence label (computed in SQL, not frontend): `running`→"In Process"; `created`/`paused` sorted by `start_at` → "Next", "Next+1", "Next+2", …; `completed`→"Completed"; `cancelled`→"Cancelled".

### Cage QR format
QR code encodes `{order_number}|{uuid}`. The `ScanCage` endpoint parses the pipe-separated string, validates the order number matches the route param, then inserts into `cages` with `cage_size` and `packages` both set to the order's `cage_size`.

### DbLoggerProvider
Registered as `ILoggerProvider`. Intercepts all log calls where the category starts with `MyDashboardApi.` and level ≥ Information. Maps category → type: `UserRepository`→USER, `OrderRepository`→PROCESS, everything else→APP. Writes async fire-and-forget to the `logs` table; swallows its own errors to avoid infinite recursion.

### Dapper mapping
`DefaultTypeMap.MatchNamesWithUnderscores = true` is set globally — `snake_case` SQL columns map to `PascalCase` C# properties. Flat result types use positional records; types with nested lists (e.g., `OrderDetail` with `List<CageEntry>`) use POCO classes.

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
- **BackchannelAuthority** — internal Keycloak endpoint for JWKS (avoids DNS/SSL in WSL)
- **Audience** — must match the `aud` claim (requires Keycloak audience mapper)

## Role enforcement

Two roles: `admin` and `viewer`. Checked server-side via `IUserRepository.GetUserContextAsync`.

| Endpoint group | Admin required |
|----------------|---------------|
| `POST /api/orders` | ✓ |
| `DELETE /api/orders/{id}` | ✓ |
| `PATCH /api/orders/{id}/status` | ✓ |
| `PATCH /api/settings/{key}` | ✓ |
| `PATCH /api/users/{id}/role` | ✓ |
| Everything else | ✗ (any authenticated user) |

Pattern for admin-only endpoints:
```csharp
var (_, role) = await users.GetUserContextAsync(keycloakId);
if (role != "admin") return Results.Forbid();
```

## User provisioning

- No signup flow — users are created in Keycloak and provisioned automatically on first login via `POST /api/me`.
- First user to ever log in gets `admin`; all subsequent new users get `viewer`.
- Role can be changed via `PATCH /api/users/{id}/role` (admin only) or directly in DB.

## Rules
- All route handlers live in `Endpoints/`, not in `Program.cs`
- All DB access goes through a repository interface — never inline SQL in endpoints
- Repositories are registered as singletons; `NpgsqlDataSource` is the shared connection pool
- `DefaultTypeMap.MatchNamesWithUnderscores = true` is set globally
- Always keep `BackchannelAuthority` pointing to `http://localhost:8080` in dev
- Background services must be registered with `AddHostedService<T>()` in `Program.cs`
- Never commit real connection string passwords to source control
