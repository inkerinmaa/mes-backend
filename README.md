# mes-backend

.NET 10 minimal API backend for the MES (Manufacturing Execution System) dashboard. Provides REST endpoints with JWT authentication (Keycloak), real-time push via SignalR, and persistent storage in PostgreSQL.

## Quick start (local dev)

```bash
# Prerequisites: .NET 10 SDK, Docker (PostgreSQL + Keycloak running)
cd ~/projects/dwh && docker compose up -d
docker exec -i postgres-db psql -U nik -d mydb < ~/projects/dwh/init.sql
docker exec -i postgres-db psql -U nik -d mydb < ~/projects/dwh/seed.sql

cd ~/projects/mes-backend
dotnet run
# Listening on http://localhost:5000
```

In local dev `Redis:ConnectionString` is empty in `appsettings.json`, so SignalR runs in-memory (fine for a single process).

---

## Full deployment on a new server

### 1. Prerequisites

- Docker Engine with Compose v2
- Git
- An existing Keycloak instance (any version ≥ 21)
- A domain or IP for the MES app (e.g. `mes.example.com`)

### 2. Clone repositories

```bash
git clone https://github.com/your-org/mes-backend.git   ~/projects/mes-backend
git clone https://github.com/your-org/mes-frontend.git  ~/projects/mes-frontend
git clone https://github.com/your-org/mes-docker.git    ~/projects/mes-docker
git clone https://github.com/your-org/dwh.git           ~/projects/dwh
```

### 3. Start PostgreSQL and apply schema

```bash
cd ~/projects/dwh
docker compose up -d
# Wait ~5 s for Postgres to initialise
docker exec -i postgres-db psql -U nik -d mydb < init.sql
docker exec -i postgres-db psql -U nik -d mydb < seed.sql
```

### 4. Configure Keycloak

Do this once in your Keycloak admin console (`https://<keycloak>/admin`).

#### 4a. Create realm

- Realm name: `mes-realm` (or any name — update env vars to match)

#### 4b. Create client `mes-frontend`

| Setting | Value |
|---------|-------|
| Client type | OpenID Connect |
| Client authentication | OFF (public client) |
| Standard flow | ON |
| Direct access grants | OFF |
| Valid redirect URIs | `https://<mes-domain>/*` |
| Valid post-logout redirect URIs | `https://<mes-domain>/*` |
| Web origins | `https://<mes-domain>` |

#### 4c. Add protocol mappers to `mes-frontend`

Go to **Clients → mes-frontend → Client scopes → mes-frontend-dedicated → Add mapper → By configuration**.

**Mapper 1 — Audience**
| Field | Value |
|-------|-------|
| Mapper type | Audience |
| Name | `mes-frontend-audience` |
| Included client audience | `mes-frontend` |
| Add to access token | ON |

**Mapper 2 — Group membership**
| Field | Value |
|-------|-------|
| Mapper type | Group Membership |
| Name | `groups` |
| Token claim name | `groups` |
| Full group path | OFF |
| Add to ID token | ON |
| Add to access token | ON |
| Add to userinfo | ON |

#### 4d. Create groups

Create two groups in **Groups**:
- `mes-admins` — users in this group get `admin` role in MES
- `mes-viewers` — users in this group get `viewer` role in MES

#### 4e. Create users

For each MES user:
1. **Users → Add user** — set username, email, enable the account
2. **Credentials** tab → set a password (disable "Temporary")
3. **Groups** tab → assign to `mes-admins` or `mes-viewers`

### 5. Configure the MES stack (.env)

```bash
cd ~/projects/mes-docker
cp .env.example .env
```

Edit `.env`:

```env
# Npgsql connection string. Use host.docker.internal when Postgres is
# a Docker container on the same host, exposed on port 5432.
POSTGRES_CONNECTION_STRING=Host=host.docker.internal;Port=5432;Database=mydb;Username=nik;Password=mysecretpassword

# Public Keycloak realm URL — must exactly match the `iss` claim in JWTs.
KEYCLOAK_AUTHORITY=https://keycloak.example.com/realms/mes-realm

# Internal URL the backend container uses to fetch JWKS.
# If Keycloak is on the same host (Docker, port 8080):
KEYCLOAK_BACKCHANNEL_AUTHORITY=http://host.docker.internal:8080/realms/mes-realm
# If Keycloak is on a different host reachable from this machine:
# KEYCLOAK_BACKCHANNEL_AUTHORITY=https://keycloak.example.com/realms/mes-realm

# Keycloak client ID (must match what you created in step 4b)
KEYCLOAK_CLIENT_ID=mes-frontend

# Host port for the nginx container
HTTP_PORT=80
```

### 6. Start the stack

```bash
cd ~/projects/mes-docker
docker compose up -d --build
```

The MES frontend is now available at `http://<server>:${HTTP_PORT}`.

### 7. Verify

1. Open `http://<server>` — you should see the login page.
2. Click "Log in with Keycloak" — you should be redirected to Keycloak and back.
3. Check `docker compose logs -f mes-backend` — you should see `User synced: <username> | Role=admin`.

---

## Role model

MES has two built-in roles: `admin` and `viewer`. Role is derived from Keycloak **group membership** on every login and stored in the `users` table.

| Keycloak group | MES role |
|---------------|----------|
| `mes-admins` | `admin` |
| `mes-viewers` | `viewer` |
| neither | `viewer` (fallback) |

If a user is in both groups, `admin` wins.

### What each role can do

| Action | admin | viewer |
|--------|-------|--------|
| View orders, dashboard, events | ✓ | ✓ |
| Add completed cage to order | ✓ | ✓ |
| Edit cage packages, order comment | ✓ | ✓ |
| Update own profile name | ✓ | ✓ |
| Configure own notification prefs | ✓ | ✓ |
| Create orders | ✓ | ✗ |
| Cancel orders | ✓ | ✗ |
| Start / Pause / Complete orders | ✓ | ✗ |
| Change global settings | ✓ | ✗ |
| Change other users' roles | ✓ | ✗ |

All role checks are enforced in the backend. The frontend hides controls for usability, not security.

### How it works technically

**Token flow:**
1. User logs in → Keycloak issues a JWT containing `"groups": ["mes-admins"]` (added by the Group Membership mapper).
2. Frontend calls `POST /api/me` → backend reads `groups` claims from the JWT and calls `UpsertUserAsync(..., groups)`.
3. `UpsertUserAsync` maps `mes-admins → "admin"`, `mes-viewers → "viewer"`, writes the role to `users.role`.
4. On every subsequent API call, endpoints call `GetUserContextAsync(keycloakId)` which reads `role` from the DB.

**Why store role in DB instead of reading the JWT every time:**
`GetUserContextAsync` is called on every admin-guarded request. Reading the DB is a single indexed lookup. It also lets an admin demote a user via the Settings UI without requiring that user to log out and back in.

**Role update timing:**
Changing a user's Keycloak group takes effect on their **next login** (when `POST /api/me` runs). For an immediate role change, use the MES Settings → Team Members page (admin only) or update `users.role` directly in the DB.

---

## Adding a new role

Example: adding a `supervisor` role that can create orders but not cancel them.

### 1. Keycloak — create group

Add group `mes-supervisors` in the Keycloak admin console.

### 2. Backend — map group to role

In `UserRepository.cs`, update `UpsertUserAsync`:

```csharp
var role = groupList.Contains("mes-admins")      ? "admin"
         : groupList.Contains("mes-supervisors") ? "supervisor"
         : groupList.Contains("mes-viewers")     ? "viewer"
         : "viewer";
```

### 3. Backend — add permission checks

In each endpoint, add a check for the new role as needed:

```csharp
// Allow admin and supervisor, block viewer
var (userId, role) = await users.GetUserContextAsync(keycloakId);
if (role != "admin" && role != "supervisor") return Results.Forbid();
```

### 4. Frontend — gate UI elements

In `useMesUser.ts`, extend `canSeeAdminUi` or add a new computed:

```ts
const canManageOrders = computed(() =>
  mesRole.value === 'admin' || mesRole.value === 'supervisor'
)
```

Then use it in templates:
```vue
<UButton v-if="canManageOrders" label="New order" ... />
```

### 5. Update realm-export.json

Add the new group to `keycloak-setup/realm-export.json`:
```json
{ "name": "mes-supervisors", "subGroups": [] }
```

---

## Adding permissions for a specific activity

Example: restricting "Add cage" to admins only (currently open to any authenticated user).

### Backend

In `Endpoints/OrderEndpoints.cs`, inside the `AddCage` handler, add a role check after resolving the user context:

```csharp
private static async Task<IResult> AddCage(
    int id, IOrderRepository orders,
    IUserRepository users, HttpContext ctx, ILogger<Program> logger)
{
    var keycloakId = ctx.User.FindFirst("sub")?.Value ?? "";
    var (userId, role) = await users.GetUserContextAsync(keycloakId);

    if (role != "admin")
        return Results.Forbid();

    // ... rest of handler
}
```

`GetUserContextAsync` hits the DB on every call (single indexed read on `keycloak_id`). It returns `(int? Id, string? Role)` — null if the user has never logged in.

### Frontend

In `orders/[id].vue`, import `useMesUser` and gate the button:

```vue
<script setup>
import { useMesUser } from '../../composables/useMesUser'
const { canSeeAdminUi } = useMesUser()
</script>

<template>
  <UButton v-if="canSeeAdminUi" label="Add Cage" @click="addCage" />
</template>
```

`canSeeAdminUi` is `true` for any role that is not explicitly `'viewer'`. For finer control, expose the role directly:

```ts
const { mesRole } = useMesUser()
// then: v-if="mesRole === 'admin'"
```

---

## User management

### Adding a user

1. Create the user in Keycloak (username, password, assign to `mes-admins` or `mes-viewers`).
2. The user logs in — `POST /api/me` upserts them into `users` with the correct role automatically.

No DB steps needed.

### Changing a user's role

**Option A — MES UI** (Settings → General → Team Members, admin only): immediate effect.

**Option B — Keycloak group** (move user between groups): takes effect on next login.

**Option C — direct DB** (use when locked out of admin):
```sql
UPDATE users SET role = 'admin' WHERE username = 'operator1';
```

---

## Data model

### `users`
Upserted on every login. `keycloak_id` (JWT `sub`) is the unique key. `role` is overwritten from Keycloak groups on every login.

| Column | Notes |
|--------|-------|
| `keycloak_id` | JWT `sub` claim |
| `email`, `username` | Overwritten on every login |
| `full_name` | Set on first login; preserved on subsequent logins (user may edit it) |
| `role` | Derived from Keycloak group membership on every login |
| `last_login` | Updated each login |
| `last_alert_ack_at` | Timestamp of last "Acknowledge All" |

### `orders`
Core production work order.

| Column | Notes |
|--------|-------|
| `status` | `created` / `running` / `paused` / `completed` / `cancelled` |
| `cage` | Whether cage tracking is enabled |
| `cage_size` | Packages per cage, set at creation, immutable |

**State machine:**
```
created ──[start]──► running ──[pause]──► paused
   │                    │                   │
   └──[cancel]──►  cancelled ◄──[cancel]────┘
                   running ──[complete]──► completed
```

### `cages`
One row per completed cage. `cage_guid` is auto-generated (`gen_random_uuid()`).

| Column | Notes |
|--------|-------|
| `cage_guid` | Auto-generated UUID |
| `cage_size` | Copied from the order at completion time |
| `packages` | Editable after creation (defaults to `cage_size`) |
| `completed_at` | Timestamp of cage completion |
| `completed_by_id` | FK to `users.id` |

### `machine_states`
Append-only timeline. Duration computed via `LEAD(ts) OVER (...) - ts`.

### `logs`
Written by `DbLoggerProvider`. Category → type mapping: `UserRepository`→USER, `OrderRepository`→PROCESS, others→APP.

### ClickHouse historian

`ReportRepository` queries `historian.production_metrics` (5-min) and `historian.energy_metrics` (15-min) via HTTP API. Results are aggregated by `order_number` using SUM/AVG, then enriched with SKU info from PostgreSQL.

| Report | Data source | Key metrics |
|--------|-------------|-------------|
| PKF | `production_metrics` | Basalt (t), Binder (kg), Wool (t), Waste (kg), Avg Efficiency (%) |
| Energy | `energy_metrics` | Gas (m³), Electricity (kWh), Water (m³) |

### `products` + setpoint tables
Product master with six related setpoint tables (`general_sp`, `saws_sp`, `tahu_sp`, `bundler_sp`, `consumables_sp`, `ul_sp`). Each has `UNIQUE (product_id)` — one row per product. `PATCH /api/products/{id}` upserts only the setpoint sections included in the request body (sections omitted are left unchanged).

### `settings`
| Key | Default |
|-----|---------|
| `timeline_auto_refresh_enabled` | `true` |
| `timeline_refresh_interval_seconds` | `60` |
| `show_efficiency_chart` | `true` |

Settings rows must pre-exist in the DB — `PATCH /api/settings/{key}` does a plain `UPDATE` (no upsert). If a key is missing the endpoint returns 404. Add new keys to `~/projects/dwh/seed.sql`.

---

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
| POST | `/api/orders/{id}/cages` | any | Add completed cage |
| PATCH | `/api/orders/{id}/cages/{cageId}/packages` | any | Edit cage packages |
| DELETE | `/api/orders/{id}/cages/{cageId}` | any | Remove cage |
| PATCH | `/api/orders/{id}/comment` | any | Update comment |
| GET | `/api/skus` | any | SKU list |
| GET | `/api/uoms` | any | Unit of measure list |
| POST | `/api/me` | any | Upsert user from JWT (called on every login) |
| PATCH | `/api/me/name` | any | Update display name |
| GET | `/api/me/notification-prefs` | any | Get alert type preferences |
| PUT | `/api/me/notification-prefs` | any | Save alert type preferences |
| GET | `/api/notifications` | any | Unacknowledged alerts |
| POST | `/api/notifications/ack` | any | Acknowledge all alerts |
| GET | `/api/members` | any | Team members |
| GET | `/api/settings` | any | Global settings |
| PATCH | `/api/settings/{key}` | **admin** | Update a setting |
| GET | `/api/users` | any | All users |
| PATCH | `/api/users/{id}/role` | **admin** | Change user role |
| POST | `/api/machine-states` | any | Insert state event + broadcast `MachineStateUpdated` |
| GET | `/api/reports/pkf?lineId=&startDate=&endDate=` | any | PKF report by period |
| GET | `/api/reports/pkf?orderNumber=` | any | PKF report for single order |
| GET | `/api/reports/energy?lineId=&startDate=&endDate=` | any | Energy report by period |
| GET | `/api/reports/energy?orderNumber=` | any | Energy report for single order |
| GET | `/api/products` | any | Product list (id, number, sku, description, code) |
| GET | `/api/products/{id}` | any | Full product detail with all six setpoint objects |
| PATCH | `/api/products/{id}` | **admin** | Update product fields + upsert setpoint sections |

---

## SignalR — `/hubs/dashboard`

JWT passed as `?access_token=` query param.

| Event | Trigger | Payload |
|-------|---------|---------|
| `OrdersUpdated` | Every 5 s | `{ value, variation }` |
| `StatsUpdated` | Every 3 s | `{ totalTonnes, lineUptime, wastePercentage, … }` |
| `ProcessDataUpdated` | Every 3 s | `{ timestamp, temperature, pressure, cycleTime, machineState }` |
| `MachineStateUpdated` | New `machine_states` row | `{ lineId }` |
| `AlertsUpdated` | Every log write | (no payload) |

### Redis backplane

Without Redis, SignalR state is in-memory per process. With multiple backend instances, a browser connected to instance A won't receive events from instance B. Redis solves this:

```
Browser ──WS──► Instance A
Browser ──WS──► Instance B

ProcessDataService (on B) broadcasts:
  → Redis Pub/Sub channel
  → Instance A subscribes → pushes to its browsers
  → Instance B pushes to its own browsers
```

`Redis:ConnectionString` is set to `redis:6379` in `docker-compose.yml`. Empty in dev → in-memory fallback.

---

## Project structure

```
mes-backend/
├── Program.cs                        DI, middleware, endpoint mapping
├── Models/
│   ├── Order.cs                      Order, OrderDetail, CageEntry, request records
│   ├── Product.cs                    ProductListItem, ProductDetail, setpoint POCOs, UpdateProductRequest
│   └── ...
├── Database/Repositories/
│   ├── IOrderRepository + OrderRepository
│   ├── IUserRepository + UserRepository
│   ├── IProductRepository + ProductRepository
│   └── ...
├── Endpoints/
│   ├── OrderEndpoints.cs
│   ├── UserEndpoints.cs
│   ├── ProductEndpoints.cs
│   ├── ReportEndpoints.cs
│   └── ...
├── Hubs/DashboardHub.cs
├── Services/
│   ├── ProcessDataService.cs         Simulates OPC UA telemetry + line state changes
│   ├── OrdersBackgroundService.cs    Simulates order events
│   └── DbLoggerProvider.cs           ILoggerProvider → logs table
└── appsettings.json
```
