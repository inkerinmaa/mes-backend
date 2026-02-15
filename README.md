# MyDashboardApi

.NET 10 Web API backend for the MES (Manufacturing Execution System) dashboard. Provides REST endpoints with JWT authentication (Keycloak), real-time updates via SignalR, and is designed to integrate with PostgreSQL, ClickHouse, and OPC UA.

## MES Architecture

```
                    +-----------------+
                    |   OPC UA Server |    (PLC / SCADA)
                    |   port 4840     |
                    +--------+--------+
                             |
              +--------------+--------------+
              |                             |
              v                             v
   +---------------------+      +------------------------+
   | ProcessDataService   |      | OpcUaCollectorService  |
   | (BackgroundService)  |      | (BackgroundService)    |
   |                      |      |                        |
   | Subscribes to OPC UA |      | Subscribes to OPC UA   |
   | monitored items,     |      | monitored items,       |
   | pushes to SignalR    |      | batch-inserts into     |
   | immediately          |      | ClickHouse             |
   +----------+-----------+      +-----------+------------+
              |                              |
              v                              v
   +---------------------+      +------------------------+
   |     SignalR Hub      |      |      ClickHouse        |
   |  /hubs/dashboard     |      |   Time-series store    |
   |                      |      |   port 8123 / 9000     |
   | "ProcessDataUpdated" |      |                        |
   | "OrdersUpdated"      |      | Historical queries:    |
   +----------+-----------+      | trends, charts,        |
              ^                  | analytics              |
              |                  +-----------+------------+
              |                              |
   +----------+-----------+                  v
   | OrdersBackgroundSvc  |      +------------------------+
   | (BackgroundService)  |      |     REST API           |
   |                      |      |  /api/process/history  |
   | Listens to Postgres  |      |  /api/process/trends   |
   | LISTEN/NOTIFY or     |      +------------------------+
   | polls for business   |
   | events               |
   +----------+-----------+
              |
              v
   +---------------------+      +------------------------+
   |     PostgreSQL       |      |     REST API           |
   |  Transactional data  |      |  /api/dashboard/*      |
   |  port 5432           |      |  /api/customers        |
   |                      |<---->|  /api/orders           |
   | Orders, customers,   |      |  /api/mails            |
   | production orders,   |      |  /api/notifications    |
   | users, config        |      |  /api/members          |
   +---------------------+      +------------------------+
```

### Data Flow Summary

| Source | Consumer | Transport | Latency | Purpose |
|--------|----------|-----------|---------|---------|
| OPC UA | SignalR (ProcessDataService) | Direct subscription | Sub-second | Live machine telemetry on dashboard |
| OPC UA | ClickHouse (OpcUaCollectorService) | Batch insert | Seconds | Historical storage for trends/analytics |
| PostgreSQL | SignalR (OrdersBackgroundService) | LISTEN/NOTIFY or poll | 1-5 seconds | Business event notifications |
| PostgreSQL | REST API | Request/response | On demand | CRUD operations, dashboard stats |
| ClickHouse | REST API | Request/response | On demand | Historical charts, trend analysis |

### Why OPC UA Direct for Real-Time (not ClickHouse)?

The key architectural decision: **SignalR reads process data directly from OPC UA, not from ClickHouse.**

| Concern | OPC UA Direct | ClickHouse Poll |
|---------|--------------|-----------------|
| Latency | Sub-second (push-based) | Seconds minimum (poll + query) |
| Database load | Zero | Constant query pressure |
| Data freshness | Immediate on value change | Depends on batch insert interval |
| Complexity | OPC UA client subscription | Query scheduling + change detection |

ClickHouse is optimized for analytical queries over large time ranges, not for sub-second real-time reads. OPC UA's subscription model pushes data on change, making it the natural source for live telemetry.

### Two Real-Time Streams, One Hub

The `DashboardHub` serves as the single convergence point for both data streams:

1. **`ProcessDataUpdated`** -- Machine telemetry from OPC UA (temperature, pressure, cycle time, machine state). Pushed every time OPC UA reports a value change. Used for live gauges, real-time charts.

2. **`OrdersUpdated`** -- Business metrics from PostgreSQL (order count, status changes). Pushed when a production order completes or status changes. Used for KPI cards, production counters.

The frontend subscribes to both events on the same SignalR connection and routes them to different UI components.

## Project Structure

```
MyDashboardApi/
├── Program.cs                  DI, middleware, endpoint mapping
├── Models/
│   ├── Dashboard.cs            StatMetric, DashboardStats, RevenuePoint, Sale
│   ├── Customer.cs             Avatar (shared), Customer
│   ├── Mail.cs                 MailSender, Mail
│   ├── Notification.cs         NotificationSender, Notification
│   └── Member.cs               Member
├── Hubs/
│   └── DashboardHub.cs         SignalR hub (business + process events)
├── Services/
│   ├── OrdersBackgroundService.cs    Business events -> SignalR
│   └── ProcessDataService.cs         OPC UA telemetry -> SignalR (mock)
├── Endpoints/
│   ├── DashboardEndpoints.cs   /api/dashboard/stats, /revenue, /sales
│   ├── CustomerEndpoints.cs    /api/customers
│   ├── MailEndpoints.cs        /api/mails
│   ├── NotificationEndpoints.cs /api/notifications
│   └── MemberEndpoints.cs      /api/members
├── appsettings.json            Auth, connection strings, OPC UA config
└── Properties/
    └── launchSettings.json     Port 5000
```

## API Endpoints

All endpoints require JWT Bearer authentication (Keycloak).

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/dashboard/stats` | KPI cards: customers, conversions, revenue, orders |
| GET | `/api/dashboard/revenue?period=&startDate=&endDate=` | Revenue chart data points |
| GET | `/api/dashboard/sales` | 5 recent sales |
| GET | `/api/customers` | 30 customers with name, email, avatar, status, location |
| GET | `/api/mails` | 8 emails with sender, subject, body |
| GET | `/api/notifications` | 5 notifications with sender and body |
| GET | `/api/members` | 5 team members |

## SignalR Events

Hub URL: `/hubs/dashboard` (requires JWT via `?access_token=` query string)

| Event | Interval | Payload | Source |
|-------|----------|---------|--------|
| `OrdersUpdated` | 5s | `{ value: int, variation: int }` | PostgreSQL (mock) |
| `ProcessDataUpdated` | 3s | `{ timestamp, temperature, pressure, cycleTime, machineState }` | OPC UA (mock) |

## Configuration

`appsettings.json`:

```json
{
    "ConnectionStrings": {
        "Postgres": "Host=localhost;Port=5432;Database=mydb;Username=nik;Password=...",
        "ClickHouse": "Host=localhost;Port=8123;Database=mydb;Username=nik;Password=..."
    },
    "OpcUa": {
        "ServerUrl": "opc.tcp://localhost:4840",
        "SubscriptionIntervalMs": 500
    },
    "Authentication": {
        "Authority": "https://keycloak.test.local/realms/mes-realm",
        "BackchannelAuthority": "http://localhost:8080/realms/mes-realm",
        "Audience": "mes-frontend"
    }
}
```

- **Authority** -- expected JWT issuer (public Keycloak URL)
- **BackchannelAuthority** -- internal Keycloak endpoint for JWKS key fetching (avoids DNS/SSL in WSL)
- **Audience** -- expected `aud` claim (requires Keycloak audience mapper)
- **ConnectionStrings** -- placeholders for future database integration
- **OpcUa** -- placeholder for future OPC UA client

## Running

```bash
# Prerequisites: .NET 10 SDK, Keycloak running on port 8080
cd ~/projects/MyDashboardApi
dotnet restore
dotnet run
# Listening on http://localhost:5000
```

Nginx proxies `https://mes.test.local/api/*` and `/hubs/*` to `localhost:5000`.

## Technology Stack

| Component | Technology | Purpose |
|-----------|-----------|---------|
| Framework | .NET 10 minimal API | REST endpoints |
| Auth | JWT Bearer (Keycloak) | Token validation |
| Real-time | SignalR | WebSocket push to frontend |
| API docs | OpenAPI | `/openapi/v1.json` in dev |

## Future: Production Integration

### Phase 1: PostgreSQL (EF Core)
- Add `Npgsql.EntityFrameworkCore.PostgreSQL`
- Create `DbContext` with entities: `Order`, `Customer`, `ProductionOrder`
- Replace mock data in endpoints with real queries
- Use `LISTEN/NOTIFY` in `OrdersBackgroundService` for real-time events

### Phase 2: OPC UA Client
- Add `OPCFoundation.NetStandard.Opc.Ua` NuGet package
- Configure monitored items (tags) in `appsettings.json`
- `ProcessDataService` subscribes to OPC UA server, pushes value changes via SignalR
- Create `OpcUaCollectorService` to batch-insert telemetry into ClickHouse

### Phase 3: ClickHouse Analytics
- Add `ClickHouse.Client` NuGet package
- Create time-series tables: `process_data (timestamp, tag, value)`
- Add endpoints: `GET /api/process/history`, `GET /api/process/trends`
- Power historical charts and trend analysis in the frontend
