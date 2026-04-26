# mes-backend

.NET 10 minimal API backend for the MES (Manufacturing Execution System) dashboard. Provides REST endpoints with JWT authentication (Keycloak), real-time push via SignalR, and persistent storage in PostgreSQL.

## Quick start (local dev)

```bash
# Prerequisites: .NET 10 SDK, Docker (PostgreSQL), Keycloak on port 8080
cd ~/projects/dwh && docker compose up -d
docker exec -i postgres-db psql -U nik -d mydb < ~/projects/dwh/init.sql

cd ~/projects/mes-backend
dotnet run
# Listening on http://localhost:5000
```

Nginx proxies `https://mes.test.local/api/*` and `/hubs/*` to `localhost:5000`.

In local dev, `Redis:ConnectionString` is empty in `appsettings.json`, so SignalR runs in-memory (single process — fine for dev).

## Docker deployment

See `docker-compose.yml` and `.env.example` in the project root (`~/projects/`).

```bash
cd ~/projects
cp .env.example .env
# Edit .env with real values
docker compose up -d --build
```

The backend image is built from `mes-backend/Dockerfile` (multi-stage: SDK build → ASP.NET runtime). All configuration is passed through environment variables which override `appsettings.json` values using .NET's double-underscore convention (`Redis__ConnectionString` → `Redis:ConnectionString`).

## Architecture

```
Frontend (Vue SPA)
        │ HTTPS / WSS
        ▼
   Nginx reverse proxy
        │
   ┌────┴──────────────────────────────────┐
   │           mes-backend                 │
   │                                       │
   │  REST Endpoints  SignalR Hub          │
   │  /api/*          /hubs/dashboard      │
   │       │               │               │
   │       ▼               ▼               │
   │  Repositories   Background services  │
   │       │         OrdersBackgroundSvc   │
   │       │         ProcessDataService    │
   │       ▼         DbLoggerProvider      │
   │  PostgreSQL                           │
   └───────────────────────────────────────┘
```

## Role model

Two roles: `admin` and `viewer`. Role is stored in the `users` table and checked server-side on every write operation.

| Action | Admin | Viewer |
|--------|-------|--------|
| View orders, dashboard, events | ✓ | ✓ |
| Scan cage QR codes | ✓ | ✓ |
| Edit cage packages, order comment | ✓ | ✓ |
| Update own profile name | ✓ | ✓ |
| Configure own notification prefs | ✓ | ✓ |
| Create orders | ✓ | ✗ |
| Cancel orders | ✓ | ✗ |
| Start / Pause / Complete orders | ✓ | ✗ |
| Change global settings | ✓ | ✗ |
| Change other users' roles | ✓ | ✗ |

All role checks are enforced in the backend — the frontend hides controls for usability only, not security.

## User management

### How users are provisioned

MES does not have its own user creation flow. Users are managed entirely in Keycloak. On first login:
1. Keycloak authenticates the user and issues a JWT.
2. The frontend calls `POST /api/me` which upserts the user into the `users` table from JWT claims.
3. **The first user to ever log in is automatically promoted to `admin`.** Every subsequent new user gets `viewer`.

There is no invite link or manual DB step needed — just create the user in Keycloak and tell them to log in.

### Adding a new user (step by step)

**In Keycloak** (`https://keycloak.test.local` → `mes-realm`):

1. Users → **Add user**
   - Username: e.g. `operator1`
   - Email: optional
   - Save

2. → **Credentials** tab → **Set password** → disable "Temporary"

3. → **Attributes** tab → Add attribute:
   - Key: `role`
   - Value: `viewer` (or `admin` for another administrator)
   - Save

That's it. The user can now log in. The `role` attribute flows into the JWT via the attribute mapper (see Keycloak setup section), gets written to the DB on first login, and is checked by the API on every write request.

### Changing a user's role after they've logged in

Option A — **via the MES UI** (Settings → General → Team Members, admin only):
- Any admin can promote/demote other users from the UI.

Option B — **directly in the DB** (if locked out of admin):
```sql
UPDATE users SET role = 'admin' WHERE username = 'operator1';
```

Option C — **via Keycloak attribute** (takes effect on next login):
- Change the `role` attribute in Keycloak → user's DB role will be overwritten on their next `POST /api/me` call.

> Note: The DB `role` column is the source of truth for API authorization. The Keycloak `role` attribute is only read at login time (via `POST /api/me`). Changing it in Keycloak does not update the DB role until the user logs in again.

## Data model

### `users`
Upserted on every login from JWT claims. `keycloak_id` (JWT `sub`) is the unique key.

| Column | Type | Notes |
|--------|------|-------|
| keycloak_id | VARCHAR | JWT sub claim |
| email, username, full_name | VARCHAR | From JWT; full_name preserved on subsequent logins |
| role | VARCHAR | `admin` or `viewer`; first user auto-promoted to admin |
| last_login | TIMESTAMPTZ | Updated each login |
| last_alert_ack_at | TIMESTAMPTZ | Timestamp of last "Acknowledge All" — alerts API filters to entries after this |

### `uom` — Unit of Measure
Reference table seeded with: `kg`, `t`, `g`, `pcs`, `m`, `m2`, `m3`, `L`.

### `skus` — Product SKUs
10 seed SKUs across Alpha, Beta, Charlie, Delta, Echo product lines.

### `production_lines`
Fixed 3 rows (id 1–3): Line 1 (primary), Line 2 (secondary), Line 3 (packaging).

### `orders`
Core production work order.

| Column | Type | Notes |
|--------|------|-------|
| order_number | VARCHAR UNIQUE | Human-readable, also FK in `cages` |
| sku_id, production_line_id, uom_id | INTEGER FK | |
| volume | DECIMAL(12,3) | Target production quantity |
| status | VARCHAR | `created` / `running` / `paused` / `completed` / `cancelled` |
| priority | VARCHAR | `High` / `Medium` / `Low` |
| planned_start_at, planned_complete_at | TIMESTAMPTZ | Set by planning at order creation; drive queue sequence |
| start_at | TIMESTAMPTZ | Auto-stamped when order first transitions to `running` |
| complete_at | TIMESTAMPTZ | Auto-stamped when order transitions to `completed` |
| cage | BOOLEAN | Whether cage tracking is enabled |
| cage_size | INTEGER | Packages per cage, set at creation, immutable |

**State machine:**
```
created ──[start]──► running ──[pause]──► paused
   │                    │                   │
   └──[cancel]──►  cancelled ◄──[cancel]──-─┘
                   running ──[complete]──► completed
```
`completed` and `cancelled` are terminal.

### `cages` — Scanned cage records
One row per QR scan. QR format: `{order_number}|{uuid}`.

### `machine_states` — Line state timeline
Append-only event log. Duration computed dynamically via `LEAD(ts) OVER (...) - ts`.

| Column | Values |
|--------|--------|
| state | `running` / `warning` / `stopped` |

### `logs` — Structured event log
Written automatically by `DbLoggerProvider`. After each write, `AlertsUpdated` is broadcast via SignalR to connected clients.

| Column | Values |
|--------|--------|
| type | `USER` / `PROCESS` / `APP` / `EQUIPMENT` / `INTEGRATION` |
| level | `DEBUG` / `INFO` / `WARNING` / `ERROR` / `CRITICAL` |

Category → type mapping: `UserRepository`→USER, `OrderRepository`→PROCESS, all others→APP.

### `settings` — Global key-value store
| Key | Default | Description |
|-----|---------|-------------|
| `timeline_auto_refresh_enabled` | `true` | Machine state timeline periodic refresh |
| `timeline_refresh_interval_seconds` | `60` | Refresh interval in seconds |

### `user_notification_prefs` — Per-user alert preferences
`(user_id, log_type)` primary key. Which log types appear in the Alerts panel. Defaults to all enabled when no rows exist for a user.

## API endpoints

All require JWT Bearer (Keycloak). Admin-only endpoints return 403 for viewers.

| Method | Path | Role | Description |
|--------|------|------|-------------|
| GET | `/api/dashboard/stats?lineId=` | any | KPI cards |
| GET | `/api/dashboard/efficiency?...&lineId=` | any | Line efficiency chart |
| GET | `/api/dashboard/events?type=&level=&limit=` | any | Recent log entries |
| GET | `/api/dashboard/states?lineId=` | any | Machine state timeline |
| GET | `/api/logs?type=&level=&limit=` | any | Filtered log entries |
| GET | `/api/orders` | any | All orders |
| POST | `/api/orders` | **admin** | Create order |
| DELETE | `/api/orders/{id}` | **admin** | Cancel order |
| PATCH | `/api/orders/{id}/status` | **admin** | Transition status (`start`/`pause`/`complete`) |
| GET | `/api/orders/{id}` | any | Order detail + cage list |
| POST | `/api/orders/{id}/cages` | any | Scan cage QR |
| PATCH | `/api/orders/{id}/cages/{cageId}/packages` | any | Edit cage packages |
| DELETE | `/api/orders/{id}/cages/{cageId}` | any | Remove cage |
| PATCH | `/api/orders/{id}/comment` | any | Update comment |
| GET | `/api/skus` | any | SKU list |
| GET | `/api/uoms` | any | Unit of measure list |
| POST | `/api/me` | any | Upsert user from JWT |
| PATCH | `/api/me/name` | any | Update display name |
| GET | `/api/me/notification-prefs` | any | Get alert type preferences |
| PUT | `/api/me/notification-prefs` | any | Save alert type preferences |
| GET | `/api/notifications` | any | Unacknowledged alerts (filtered by prefs) |
| POST | `/api/notifications/ack` | any | Acknowledge all alerts |
| GET | `/api/members` | any | Team members (mock) |
| GET | `/api/settings` | any | Global settings |
| PATCH | `/api/settings/{key}` | **admin** | Update a setting |
| GET | `/api/users` | any | All users |
| PATCH | `/api/users/{id}/role` | **admin** | Change user role |
| POST | `/api/machine-states` | any | Insert state event + broadcast `MachineStateUpdated` |

## SignalR — `/hubs/dashboard`

JWT passed as `?access_token=` query param.

| Event | Trigger | Payload |
|-------|---------|---------|
| `OrdersUpdated` | Every 5 s | `{ value, variation }` |
| `StatsUpdated` | Every 3 s | `{ totalTonnes, lineUptime, wastePercentage, … }` |
| `ProcessDataUpdated` | Every 3 s | `{ timestamp, temperature, pressure, cycleTime, machineState }` |
| `MachineStateUpdated` | New `machine_states` row | `{ lineId }` |
| `AlertsUpdated` | Every log write | (no payload) — clients re-fetch `/api/notifications` |

### Redis SignalR backplane

Without Redis, SignalR state is in-memory per process. With a single backend instance this is fine. With multiple instances (Docker replicas, k8s pods), a browser connected to instance A won't receive events broadcast by instance B.

**How the backplane works:**

```
Browser ──WS──► Instance A
Browser ──WS──► Instance B

ProcessDataService (on Instance B) calls:
  hub.Clients.All.SendAsync("StatsUpdated", data)
      │
      ▼
  StackExchange.Redis publishes to Redis channel "signalr:mes-backend"
      │
      ├──► Instance A subscribes → pushes to its connected browsers
      └──► Instance B pushes to its own connected browsers
```

Every backend instance subscribes to the same Redis Pub/Sub channels. A broadcast on any instance fans out through Redis to all instances, which then each push to their local WebSocket connections.

Configured in `Program.cs`:
```csharp
var redisConn = builder.Configuration["Redis:ConnectionString"];
var signalR = builder.Services.AddSignalR();
if (!string.IsNullOrEmpty(redisConn))
    signalR.AddStackExchangeRedis(redisConn);
```

When `Redis:ConnectionString` is empty (local dev), SignalR falls back to in-memory — no Redis needed for a single process.

## Deploying with a different Keycloak

### What to do in Keycloak

1. **Create a realm** — e.g. `mes-realm`.

2. **Create a client** named `mes-frontend`:
   - Client authentication: **OFF** (public client)
   - Standard flow: **ON**, Direct access grants: **OFF**
   - Valid redirect URIs: `https://<your-domain>/auth/callback`
   - Valid post-logout redirect URIs: `https://<your-domain>`
   - Web origins: `https://<your-domain>`

3. **Add an audience mapper** so `aud: mes-frontend` appears in tokens:
   - Client → `mes-frontend` → Client scopes → `mes-frontend-dedicated` → Add mapper → **Audience**
   - Included client audience: `mes-frontend` → Add to access token: **ON**

4. **Add a `role` attribute mapper** so the backend can read role from JWT:
   - Client → `mes-frontend` → Client scopes → `mes-frontend-dedicated` → Add mapper → **User Attribute**
   - User attribute: `role` | Token claim name: `role` | Claim JSON type: String | Add to access token: **ON**

5. **Create users** with `role = admin` or `role = viewer` in their Attributes tab.

### What to change in the backend

```json
// appsettings.json (or appsettings.Production.json)
{
  "Authentication": {
    "Authority": "https://<keycloak-public-url>/realms/<realm>",
    "BackchannelAuthority": "http://<keycloak-internal-url>/realms/<realm>",
    "Audience": "mes-frontend"
  }
}
```

| Setting | Purpose |
|---------|---------|
| `Authority` | Must match the `iss` claim in JWTs — use the **public** URL |
| `BackchannelAuthority` | Internal URL for JWKS fetch; use direct address if public hostname isn't reachable from the server |
| `Audience` | Must match the Keycloak client ID |

### What to change in the frontend

Update `authority` in `src/composables/useAuth.ts`:
```ts
authority: "https://<keycloak-public-url>/realms/<realm>",
```

### Deployment checklist

- [ ] Realm and client `mes-frontend` created
- [ ] Redirect / post-logout URIs set to the new domain
- [ ] Audience mapper added
- [ ] `role` attribute mapper added
- [ ] Users created with `role` attribute set
- [ ] Backend `Authority`, `BackchannelAuthority`, `Audience`, DB connection string updated
- [ ] Frontend `useAuth.ts` `authority` updated and frontend rebuilt

## Project structure

```
mes-backend/
├── Program.cs                        DI, middleware, endpoint mapping
├── Models/
│   ├── Dashboard.cs                  KPI and efficiency types
│   ├── Order.cs                      Order, OrderDetail, CageEntry, request records,
│   │                                 UserNotificationPref, AppSetting
│   ├── LogEntry.cs
│   └── Member.cs
├── Database/Repositories/
│   ├── IOrderRepository + OrderRepository
│   ├── IUserRepository + UserRepository   (incl. notification prefs + ack)
│   ├── ISkuRepository + SkuRepository
│   ├── IUomRepository + UomRepository
│   ├── ILogRepository + LogRepository
│   ├── IMachineStateRepository + MachineStateRepository
│   └── ISettingsRepository + SettingsRepository
├── Endpoints/
│   ├── DashboardEndpoints.cs
│   ├── OrderEndpoints.cs
│   ├── MachineStateEndpoints.cs
│   ├── NotificationEndpoints.cs
│   ├── SettingsEndpoints.cs
│   ├── SkuEndpoints.cs / UomEndpoints.cs
│   └── UserEndpoints.cs / MemberEndpoints.cs
├── Hubs/DashboardHub.cs
├── Services/
│   ├── OrdersBackgroundService.cs    Simulates order events every 5 s
│   ├── ProcessDataService.cs         Simulates OPC UA telemetry every 3 s;
│   │                                 randomly transitions a line state every ~30 s
│   └── DbLoggerProvider.cs           ILoggerProvider → logs table + AlertsUpdated broadcast
└── appsettings.json
```
