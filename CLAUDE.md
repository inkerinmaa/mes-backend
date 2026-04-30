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
- HTTP smoke tests: open `MyDashboardApi.http` in VS Code REST Client
- Docker image: `docker build -t mes-backend .` (from `mes-backend/`)
- Full stack: `docker compose up -d --build` (from `~/projects/mes-docker/`)

## Architecture
- `Program.cs` — DI setup, middleware pipeline, endpoint mapping
- `Endpoints/` — minimal API route handlers (one file per domain)
- `Models/` — DTOs and request types
- `Database/Repositories/` — Dapper-based repository implementations
  - `IOrderRepository` / `OrderRepository` — order CRUD + cage completion
  - `IUserRepository` / `UserRepository` — user upsert, role management
  - `ISkuRepository` / `SkuRepository` — SKU lookup
  - `IUomRepository` / `UomRepository` — unit-of-measure lookup
  - `ILogRepository` / `LogRepository` — structured app event log (read + write)
  - `IMachineStateRepository` / `MachineStateRepository` — production line state timeline
- `Hubs/DashboardHub.cs` — SignalR hub at `/hubs/dashboard`
- `Services/` — background services and custom logging
  - `ProcessDataService.cs` — simulates OPC UA telemetry every 3 s; also randomly transitions one production line to a new state every ~30 s, writes it to `machine_states`, and broadcasts `MachineStateUpdated { lineId }` via SignalR.
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
| `users` | `keycloak_id`, `email`, `username`, `full_name`, `role`, `last_login` | Upserted on every login; role overwritten from Keycloak groups each time |
| `uom` | `code` (kg/t/pcs/m/m2/m3/L/g), `name`, `type` | Unit of measure reference |
| `skus` | `code`, `name`, `description`, `unit` | Product SKU catalogue |
| `production_lines` | `id` (1–3), `name`, `status` | Fixed 3 lines |
| `materials` | `code`, `name`, `unit`, `stock_quantity` | Raw material inventory |
| `orders` | `order_number`, `sku_id`, `production_line_id`, `volume`, `uom_id`, `status`, `priority`, `due_date`, `start_at`, `finish_at`, `comment`, `cage`, `cage_size`, `produced_volume`, `pkg_produced`, `created_by_id`, `created_at`, `updated_at` | Status: `created`→`running`→`paused`→`running`→`completed`/`cancelled`. `cage=true` enables cage tracking for `pkg` orders. `produced_volume` tracks output in the order's UOM. `pkg_produced` is discrete package count — for `pkg` orders equals `SUM(cages.packages)`; for other UOMs randomly set on creation (updated later by OPC UA/NATS). |
| `cages` | `order_number`, `cage_guid` (auto UUID), `cage_size`, `packages`, `completed_at`, `completed_by_id` | One row per completed cage; `cage_guid` is `gen_random_uuid()`; only `packages` is editable after creation |
| `machine_states` | `id`, `production_line_id`, `state` (running/warning/stopped), `ts` | Append-only event log; duration computed via `LEAD(ts) OVER (...) - ts`; `id` returned by `InsertStateAsync` for linking events |
| `production_events` | `id`, `line_id`, `order_id?`, `machine_state_id?`, `event_type`, `severity`, `title`, `description`, `start_at`, `end_at?`, `created_by_id`, `created_at` | Operator-created annotations; `machine_state_id` links to an auto-detected stop |
| `logs` | `type` (USER/PROCESS/APP/EQUIPMENT/INTEGRATION), `message`, `level` (DEBUG/INFO/WARNING/ERROR/CRITICAL), `ts` | Written by `DbLoggerProvider` for all `MyDashboardApi.*` log lines ≥ INFO |

## Role model

Two roles: `admin` and `viewer`. Role is **derived from Keycloak group membership** on every login and stored in `users.role`.

| Keycloak group | MES role |
|---------------|----------|
| `mes-admins` | `admin` |
| `mes-viewers` | `viewer` |
| neither | `viewer` (fallback) |
| both | `admin` wins |

### How it flows

1. Keycloak issues JWT with `"groups": ["mes-admins"]` (added by the Group Membership mapper on `mes-frontend` client).
2. `POST /api/me` → `UserEndpoints.SyncUser` reads `ctx.User.Claims.Where(c => c.Type == "groups")` and calls `UpsertUserAsync(..., groups)`.
3. `UpsertUserAsync` maps group → role string, writes to `users.role` via `ON CONFLICT ... DO UPDATE SET role = EXCLUDED.role`.
4. On every admin-guarded request, endpoints call `GetUserContextAsync(keycloakId)` — single indexed DB lookup on `keycloak_id`.

### Endpoint role check pattern

```csharp
var (_, role) = await users.GetUserContextAsync(keycloakId);
if (role != "admin") return Results.Forbid();
```

### Admin-guarded endpoints

| Endpoint | Reason |
|----------|--------|
| `POST /api/orders` | Creates work orders |
| `DELETE /api/orders/{id}` | Cancels orders |
| `PATCH /api/orders/{id}/status` | Transitions order state |
| `PATCH /api/settings/{key}` | Global settings |
| `PATCH /api/users/{id}/role` | Role management |

### Adding a new role

1. Create Keycloak group (e.g. `mes-supervisors`)
2. In `UserRepository.UpsertUserAsync`: extend the group→role mapping chain
3. In endpoint handlers: add the new role to the allowed set where needed
4. In frontend `useMesUser.ts`: add a computed if finer UI gating is needed
5. Add group to `keycloak-setup/realm-export.json`

### Adding/changing permissions for an activity

**Backend** — add/remove role check in the relevant handler in `Endpoints/`:
```csharp
var (userId, role) = await users.GetUserContextAsync(keycloakId);
if (role != "admin") return Results.Forbid();
```

**Frontend** — gate the UI element with `canSeeAdminUi` from `useMesUser`:
```vue
<UButton v-if="canSeeAdminUi" label="Add Cage" @click="addCage" />
```
`canSeeAdminUi` is `true` for any role that is not explicitly `'viewer'`. For exact-role checks: `v-if="mesRole === 'admin'"`.

## Endpoints (all require JWT)

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/dashboard/stats?lineId=` | KPI cards (mock, line-scoped) |
| GET | `/api/dashboard/efficiency?period=&startDate=&endDate=&lineId=` | Line efficiency chart (mock) |
| GET | `/api/dashboard/events?type=&level=&limit=` | Last N log entries from `logs` table |
| GET | `/api/dashboard/states?lineId=&hours=` | Machine state timeline; `hours` is window size (default 8) — backend computes `from = NOW() - hours` |
| GET | `/api/dashboard/current-orders` | Current order queue snapshot (mock) |
| GET | `/api/logs?type=&level=&limit=` | Filtered log entries from `logs` table (admin only) |
| GET | `/api/orders` | All non-cancelled orders (with produced_packages) |
| POST | `/api/orders` | Create a new work order (admin) |
| DELETE | `/api/orders/{id}` | Cancel an order (admin) |
| PATCH | `/api/orders/{id}/status` | Transition order state `{ action: "start" \| "pause" \| "complete" }` (admin) |
| GET | `/api/orders/{id}` | Order detail with cage list |
| POST | `/api/orders/{id}/cages` | Add completed cage (no body — guid auto-generated) |
| PATCH | `/api/orders/{id}/cages/{cageId}/packages` | Update packages count for a cage |
| DELETE | `/api/orders/{id}/cages/{cageId}` | Remove a cage |
| PATCH | `/api/orders/{id}/comment` | Update order comment |
| GET | `/api/skus` | All SKUs |
| GET | `/api/uoms` | All units of measure |
| POST | `/api/me` | Upsert current user from JWT claims (called on every login) |
| PATCH | `/api/me/name` | Update display name |
| GET | `/api/members` | Team members |
| GET | `/api/notifications` | Alerts |
| POST | `/api/notifications/ack` | Acknowledge alerts |
| POST | `/api/machine-states` | Insert a machine state event `{ lineId, state }` and broadcast `MachineStateUpdated` via SignalR (also `StopInserted` when state=stopped) |
| GET | `/api/events?lineId=&eventType=&severity=&limit=` | Production events log |
| GET | `/api/events/unacknowledged` | Stopped states in last 24 h with no linked production event |
| POST | `/api/events` | Create a production event (all authenticated users) |
| PATCH | `/api/events/{id}/close` | Close an open event `{ endAt?, description? }` |

## SignalR events (`/hubs/dashboard`)
- `OrdersUpdated` — every 5 s, `{ value, variation }`
- `ProcessDataUpdated` — every 3 s, `{ timestamp, temperature, pressure, cycleTime, machineState }`
- `StatsUpdated` — every 3 s, `{ totalTonnes, lineUptime, wastePercentage, … }`
- `MachineStateUpdated` — on every new `machine_states` row, `{ lineId }`
- `StopInserted` — when a new `stopped` state is inserted, `{ lineId }` (triggers events page to refresh unacknowledged stops)

## Key implementation details

### Order state machine

```
created ──[start]──► running ──[pause]──► paused
   │                    │                   │
   └──[cancel]──►  cancelled ◄──[cancel]────┘
                   running ──[complete]──► completed
```

- `start`: `created` or `paused` → `running`
- `pause`: `running` → `paused`
- `complete`: `running` → `completed`
- Cancel: any non-terminal state
- Sequence label (computed in SQL): `running`→"In Process"; `created`/`paused` sorted by `planned_start_at` → "Next", "Next+1", …

### Cage completion
`POST /api/orders/{id}/cages` — no request body. Server auto-generates `cage_guid` via `gen_random_uuid()` in the INSERT. `completed_by_id` is set from the JWT sub. `packages` defaults to `cage_size`.

### DbLoggerProvider
Intercepts `MyDashboardApi.*` log lines ≥ Information. Maps category → type: `UserRepository`→USER, `OrderRepository`→PROCESS, everything else→APP. Fire-and-forget; swallows own errors.

### Dapper mapping
`DefaultTypeMap.MatchNamesWithUnderscores = true` is set globally — `snake_case` SQL columns map to `PascalCase` C# properties. Flat results use positional records; types with nested lists (e.g. `OrderDetail`) use POCO classes.

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
- **Authority** — must match the `iss` claim in the JWT (public hostname)
- **BackchannelAuthority** — internal URL for JWKS fetch (avoids DNS/SSL in WSL/Docker)
- **Audience** — must match the `aud` claim (requires Keycloak audience mapper)

## User provisioning

- Users are created in Keycloak, assigned to `mes-admins` or `mes-viewers` group.
- On first login, `POST /api/me` creates the user row; role is derived from groups.
- On every subsequent login, role is re-derived and overwritten (group change takes effect at next login).
- Role can also be changed immediately via `PATCH /api/users/{id}/role` (admin only) — updates `users.role` directly, no re-login needed.

## Docker & configuration

All `appsettings.json` values can be overridden by environment variables using .NET's double-underscore convention:

| appsettings.json key | Environment variable |
|----------------------|----------------------|
| `ConnectionStrings:Postgres` | `ConnectionStrings__Postgres` |
| `Authentication:Authority` | `Authentication__Authority` |
| `Authentication:BackchannelAuthority` | `Authentication__BackchannelAuthority` |
| `Authentication:Audience` | `Authentication__Audience` |
| `Redis:ConnectionString` | `Redis__ConnectionString` |

`Redis:ConnectionString` empty → SignalR in-memory (single instance, dev).
`Redis:ConnectionString = redis:6379` → Redis backplane active (multi-instance, prod).

## Rules
- All route handlers live in `Endpoints/`, not in `Program.cs`
- All DB access goes through a repository interface — never inline SQL in endpoints
- Repositories are registered as singletons; `NpgsqlDataSource` is the shared connection pool
- `DefaultTypeMap.MatchNamesWithUnderscores = true` is set globally
- Always keep `BackchannelAuthority` pointing to `http://localhost:8080` in dev
- Background services must be registered with `AddHostedService<T>()` in `Program.cs`
- Never commit real connection string passwords to source control
