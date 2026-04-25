# MyDashboardApi

.NET 10 minimal API backend for the MES (Manufacturing Execution System) dashboard. Provides REST endpoints with JWT authentication (Keycloak), real-time push via SignalR, and persistent storage in PostgreSQL.

## Quick start

```bash
# Prerequisites: .NET 10 SDK, Docker (PostgreSQL), Keycloak on port 8080
cd ~/projects/dwh && docker compose up -d
docker exec -i postgres-db psql -U nik -d mydb < ~/projects/dwh/init.sql

cd ~/projects/MyDashboardApi
dotnet run
# Listening on http://localhost:5000
```

Nginx proxies `https://mes.test.local/api/*` and `/hubs/*` to `localhost:5000`.

## Architecture

```
Frontend (Vue SPA)
        │ HTTPS / WSS
        ▼
   Nginx reverse proxy
        │
   ┌────┴──────────────────────────────────┐
   │           MyDashboardApi              │
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

## Data model

### `users`
Upserted on every login from JWT claims. `keycloak_id` (JWT `sub`) is the unique key.

| Column | Type | Notes |
|--------|------|-------|
| keycloak_id | VARCHAR | JWT sub claim |
| email, username, full_name | VARCHAR | From JWT, full_name preserved across logins |
| role | VARCHAR | `admin` or `viewer` |
| last_login | TIMESTAMPTZ | Updated each login |

### `uom` — Unit of Measure
Reference table seeded with: `kg`, `t`, `g`, `pcs`, `m`, `m2`, `m3`, `L`.

| Column | Type |
|--------|------|
| code | VARCHAR UNIQUE |
| name | VARCHAR |
| type | VARCHAR — `weight` / `count` / `length` / `area` / `volume` |

### `skus` — Product SKUs
10 seed SKUs across Alpha, Beta, Charlie, Delta, Echo product lines.

### `production_lines`
Fixed 3 rows (id 1–3): Line 1 (primary), Line 2 (secondary), Line 3 (packaging).

### `orders`
Core production work order.

| Column | Type | Notes |
|--------|------|-------|
| order_number | VARCHAR UNIQUE | Human-readable, also used as FK in cages |
| sku_id | INTEGER FK | |
| production_line_id | INTEGER FK | |
| volume | DECIMAL(12,3) | Target production quantity |
| uom_id | INTEGER FK | Unit for volume |
| status | VARCHAR | `queued` / `in_progress` / `completed` / `cancelled` |
| priority | VARCHAR | `High` / `Medium` / `Low` |
| cage | BOOLEAN | Whether cage tracking is enabled |
| cage_size | INTEGER | Packages per cage, set at creation, immutable |
| comment | TEXT | |

### `cages` — Scanned cage records
One row per QR scan. QR format: `{order_number}|{uuid}`.

| Column | Type | Notes |
|--------|------|-------|
| order_number | VARCHAR FK | References orders(order_number) |
| cage_guid | UUID | From QR code |
| cage_size | INTEGER | Copied from order at scan time |
| packages | INTEGER | Editable after scan (actual vs configured) |
| scanned_by_id | INTEGER FK | |

### `machine_states` — Line state timeline
Append-only event log: one row per state change. Duration is not stored — it is computed dynamically in SQL using `LEAD(ts) OVER (ORDER BY ts) - ts`; the still-active segment uses `NOW() - ts`. Seeded with ~10 events per line covering the last 8 hours.

| Column | Type | Notes |
|--------|------|-------|
| production_line_id | INTEGER FK | 1, 2, or 3 |
| state | VARCHAR | `running` / `warning` / `stopped` |
| ts | TIMESTAMPTZ | Moment of state change |

Queried by `GET /api/dashboard/states?lineId=`. The query includes the last event before the 8-hour window so the timeline bar has no gap at its left edge. New rows can be inserted via `POST /api/machine-states`, which also broadcasts `MachineStateUpdated` via SignalR.

### `logs` — Structured event log
Written automatically by `DbLoggerProvider` for all `MyDashboardApi.*` log calls ≥ INFO. Also queryable via API.

| Column | Type | Values |
|--------|------|--------|
| type | VARCHAR | `USER` / `PROCESS` / `APP` / `EQUIPMENT` / `INTEGRATION` |
| level | VARCHAR | `DEBUG` / `INFO` / `WARNING` / `ERROR` / `CRITICAL` |
| message | TEXT | Formatted log message |
| ts | TIMESTAMPTZ | |

Category → type mapping: `UserRepository`→USER, `OrderRepository`→PROCESS, all others→APP.

## API endpoints

All require JWT Bearer (Keycloak).

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/dashboard/stats?lineId=` | KPI cards (mock, line-scoped) |
| GET | `/api/dashboard/efficiency?period=&startDate=&endDate=&lineId=` | Line efficiency chart data |
| GET | `/api/dashboard/events?type=&level=&limit=` | Recent log entries (last 5 by default) |
| GET | `/api/dashboard/states?lineId=` | Machine state timeline from DB |
| GET | `/api/dashboard/current-orders` | Order queue snapshot (mock) |
| GET | `/api/logs?type=&level=&limit=` | Filtered log entries |
| GET | `/api/orders` | All active orders with produced_packages |
| POST | `/api/orders` | Create order |
| DELETE | `/api/orders/{id}` | Cancel order (admin) |
| GET | `/api/orders/{id}` | Order detail + cage list |
| POST | `/api/orders/{id}/cages` | Scan cage QR `{ qrData: "WO-001\|uuid" }` |
| PATCH | `/api/orders/{id}/cages/{cageId}/packages` | Edit cage packages |
| DELETE | `/api/orders/{id}/cages/{cageId}` | Remove cage |
| PATCH | `/api/orders/{id}/comment` | Update comment |
| GET | `/api/skus` | SKU list |
| GET | `/api/uoms` | Unit of measure list |
| POST | `/api/me` | Upsert user from JWT |
| PATCH | `/api/me/name` | Update display name |
| GET | `/api/members` | Team members (mock) |
| GET | `/api/notifications` | Notifications (mock) |
| POST | `/api/machine-states` | Insert a state event `{ lineId, state }` + broadcast `MachineStateUpdated` |

## SignalR — `/hubs/dashboard`

JWT passed as `?access_token=` query param (WebSocket upgrades can't carry headers).

| Event | Frequency | Payload |
|-------|-----------|---------|
| `OrdersUpdated` | 5 s | `{ value, variation }` |
| `StatsUpdated` | 3 s | `{ totalTonnes, lineUptime, wastePercentage, … }` |
| `ProcessDataUpdated` | 3 s | `{ timestamp, temperature, pressure, cycleTime, machineState }` |
| `MachineStateUpdated` | on new DB row | `{ lineId }` — sent by `ProcessDataService` (~30 s simulation) and by `POST /api/machine-states` |

## Deploying with a different Keycloak

### What to do in Keycloak

1. **Create a realm** — e.g. `mes-realm`. The realm name becomes part of the issuer URL so pick it once and keep it consistent.

2. **Create a client** named `mes-frontend` (this name must match the `Audience` setting in the backend):
   - Client authentication: **OFF** (public client — the browser app has no secret)
   - Standard flow: **ON**, Direct access grants: **OFF**
   - Valid redirect URIs: `https://<your-domain>/auth/callback`
   - Valid post logout redirect URIs: `https://<your-domain>`
   - Web origins: `https://<your-domain>` (enables CORS for token requests)

3. **Add an audience mapper** — without this the JWT's `aud` claim won't contain `mes-frontend` and every request will be rejected with 401:
   - Client → `mes-frontend` → Client scopes → `mes-frontend-dedicated` → Add mapper → By configuration → **Audience**
   - Name: anything (e.g. `mes-frontend-audience`)
   - Included client audience: `mes-frontend`
   - Add to access token: **ON**

4. **Create a `role` attribute mapper** so the backend can read the user's role from the JWT:
   - Client → `mes-frontend` → Client scopes → `mes-frontend-dedicated` → Add mapper → By configuration → **User Attribute**
   - Name: `role`
   - User attribute: `role`
   - Token claim name: `role`
   - Claim JSON type: String
   - Add to access token: **ON**

   Then set `role = admin` or `role = viewer` on each user under Users → Attributes.

5. **Create test users** with the `role` attribute set.

### What to change in the backend

All authentication config lives in `appsettings.json` under `Authentication`:

```json
"Authentication": {
  "Authority": "https://<keycloak-public-hostname>/realms/<realm-name>",
  "BackchannelAuthority": "http://<keycloak-internal-hostname>/realms/<realm-name>",
  "Audience": "mes-frontend"
}
```

| Setting | Purpose | Example |
|---------|---------|---------|
| `Authority` | Must match the `iss` claim in every JWT. Use the **public** URL — the one Keycloak puts in tokens. | `https://auth.example.com/realms/mes-realm` |
| `BackchannelAuthority` | Used by the backend to fetch the JWKS (public keys) for token verification. Use an **internal/direct** URL if the public hostname isn't reachable from the server (e.g. different network, self-signed cert). If omitted or set to the same as `Authority`, the backend just uses `Authority`. | `http://keycloak:8080/realms/mes-realm` |
| `Audience` | Must match the Keycloak client ID and the `aud` claim produced by the audience mapper. | `mes-frontend` |

> **`Authority` vs `BackchannelAuthority`:** The frontend browser must reach `Authority` to do the OIDC redirect. The backend never talks to `Authority` directly for end-user flows — it only needs `BackchannelAuthority` to download the public key once and then verify JWTs locally. Splitting them is useful when Keycloak is behind a reverse proxy or on an internal network where the DNS name differs from the public URL.

In production use `appsettings.Production.json` (or environment variables) rather than editing `appsettings.json` directly:

```json
// appsettings.Production.json
{
  "ConnectionStrings": {
    "Postgres": "Host=db.internal;Database=mes;Username=mes;Password=<secret>"
  },
  "Authentication": {
    "Authority": "https://auth.example.com/realms/mes-realm",
    "BackchannelAuthority": "http://keycloak.internal:8080/realms/mes-realm",
    "Audience": "mes-frontend"
  }
}
```

.NET merges this on top of `appsettings.json` automatically when `ASPNETCORE_ENVIRONMENT=Production`.

### What to change in the frontend

The frontend has Keycloak coordinates hardcoded in `src/composables/useAuth.ts`:

```ts
authority: "https://keycloak.test.local/realms/mes-realm",
client_id: "mes-frontend",
redirect_uri: `${window.location.origin}/auth/callback`,
```

Update `authority` to your new realm's public URL. `redirect_uri` is dynamic (`window.location.origin`) so it adapts to the domain automatically. `client_id` must match the Keycloak client name.

### Checklist

- [ ] Keycloak realm created
- [ ] Client `mes-frontend` created (public, PKCE/standard flow)
- [ ] Redirect and post-logout URIs set to the new domain
- [ ] Audience mapper added → `aud: mes-frontend` in tokens
- [ ] `role` attribute mapper added → role readable from JWT
- [ ] Users created with `role` attribute set
- [ ] Backend `appsettings.Production.json` updated: `Authority`, `BackchannelAuthority`, `Audience`, `Postgres` connection string
- [ ] Frontend `useAuth.ts` `authority` updated and frontend rebuilt

## Project structure

```
MyDashboardApi/
├── Program.cs                        DI, middleware, endpoint mapping
├── Models/
│   ├── Dashboard.cs                  StatMetric, MachineState, EfficiencyPoint, …
│   ├── Order.cs                      Order, OrderDetail, CageEntry, Uom, request records
│   ├── LogEntry.cs                   LogEntry
│   ├── Member.cs / Notification.cs   Mock model types
├── Database/Repositories/
│   ├── IOrderRepository + OrderRepository
│   ├── IUserRepository + UserRepository
│   ├── ISkuRepository + SkuRepository
│   ├── IUomRepository + UomRepository
│   ├── ILogRepository + LogRepository
│   └── IMachineStateRepository + MachineStateRepository
├── Endpoints/
│   ├── DashboardEndpoints.cs         /api/dashboard/* + /api/logs
│   ├── OrderEndpoints.cs             /api/orders/*
│   ├── MachineStateEndpoints.cs      POST /api/machine-states (insert + SignalR broadcast)
│   ├── SkuEndpoints.cs / UomEndpoints.cs
│   └── UserEndpoints.cs / MemberEndpoints.cs / NotificationEndpoints.cs
├── Hubs/DashboardHub.cs              SignalR hub
├── Services/
│   ├── OrdersBackgroundService.cs    Simulates order events every 5 s → OrdersUpdated
│   ├── ProcessDataService.cs         Simulates OPC UA telemetry every 3 s → ProcessDataUpdated + StatsUpdated;
│   │                                 also randomly transitions a line state every ~30 s → machine_states DB + MachineStateUpdated
│   └── DbLoggerProvider.cs           ILoggerProvider → logs table
└── appsettings.json
```

## Technology stack

| Component | Technology |
|-----------|-----------|
| Framework | .NET 10 minimal API |
| Auth | JWT Bearer (Keycloak OIDC) |
| Real-time | SignalR |
| Database | PostgreSQL 18 via Npgsql 9 + Dapper |
| Logging | Microsoft.Extensions.Logging + custom DbLoggerProvider |
