# POS Inventory Service

Multi-tenant inventory management for the POS SaaS platform. The service owns stock balances, movement history, sale reservations, stock adjustments, branch transfers, customer-return restocking and low-stock alerts.

Built with **ASP.NET Core / .NET 10**, **EF Core + SQL Server**, **MediatR**, **FluentValidation**, **AutoMapper**, **OpenIddict validation**, **Serilog** and **RabbitMQ**. The HTTP API is versioned under `/api/v1`.

## Project structure

| Project | Responsibility |
|---|---|
| `Pos.InventoryService.Domain` | Entities and string constants for statuses, movement types and references |
| `Pos.InventoryService.Application` | Commands, queries, handlers, validators, DTOs, mapping, interfaces and result wrappers |
| `Pos.InventoryService.Infrastructure.Persistence` | EF context/configurations, migrations, repositories, query extensions, item validation, low-stock evaluation and unit of work |
| `Pos.InventoryService.Infrastructure.Shared` | Reservation release service, expiration worker, RabbitMQ publisher and outbox processing |
| `Pos.InventoryService.WebApi` | Versioned controllers, authentication, role policies, current-user claims, error middleware and Swagger |
| `Pos.InventoryService.IntegrationTests` | Handler/repository integration tests, HTTP tests and SQL Server guarantees |

Controllers send requests through MediatR validation to handlers. Repositories stage changes in a shared scoped `ApplicationDbContext`. `IUnitOfWork.SaveChangesAsync` evaluates changed balances before saving the operation.

## Inventory model

A balance identifies one **tenant + branch + product + optional variant**.

```text
AvailableQuantity = QuantityOnHand - QuantityReserved
```

- **On-hand:** stock physically recorded at the branch.
- **Reserved:** stock held for sales awaiting completion.
- **Available:** stock that can still be reserved or dispatched.
- **Threshold:** the on-hand quantity at or below which an alert becomes active.

A null variant identifies a product without variants; it is not a wildcard for all variants. Quantities use `decimal(18,3)`. Whole-unit products reject fractional quantities.

| Entity | Purpose |
|---|---|
| `StockBalance` | Current quantities and threshold for an item at a branch |
| `StockMovement` | On-hand change history: delta, before/after values, actor and source reference |
| `StockReservation` / `StockReservationItem` | A sale's temporary hold and item quantities |
| `StockAdjustment` / `StockAdjustmentItem` | Count correction workflow and original balance snapshot |
| `StockTransfer` / `StockTransferItem` | Branch transfer workflow and cumulative received quantities |
| `LowStockAlert` | Active or resolved low-stock episode |
| `OutboxMessage` | Event persisted with a business operation and awaiting publication |

Inventory owns the `inventory` database schema. Its item-validation service reads these external contracts:

- `branch.Branches`
- `Catalog.Products`
- `Catalog.ProductVariants`
- `Catalog.Units`

These EF read models use `ToView`; their backing tables/views must exist in the configured database and are not managed by Inventory migrations. Validation checks active tenant-owned branches/products, inventory tracking, variant ownership and unit rules. Units may belong to the tenant or be shared globally.

## Business workflows

### Opening stock

Create the initial balance and an `OpeningStock` movement in one save. Quantity must be positive and the item valid. An existing balance cannot be initialized again. Repeating the same `RequestId` with identical inputs returns the original result; changed inputs are rejected.

### Sale reservations

```text
Create → Active → Consumed
                → Released
                → Expired
```

- **Create:** validate all items and check availability. Increase reserved stock without changing on-hand. Insufficient stock rejects the entire request.
- **Consume:** decrease on-hand and reserved quantities, append `Sale` movements and mark consumed. Repeated completion does not deduct twice.
- **Release:** free reserved stock when a sale is cancelled; on-hand stays unchanged.
- **Expire:** the worker frees overdue active holds. Repeated release/expiry is harmless.

Creation accepts a `SaleId`, stored in `StockReservation.ReferenceId`. Consume/release use the **reservation's own `Id`**. Expiry is calculated by the server from `StockReservationOptions.ExpiryMinutes`.

SQL row versions protect competing updates. A conflict requires a fresh request/reload; creation has no automatic internal retry loop. The current availability check rejects insufficient stock; configurable negative-stock selling is not implemented.

### Stock adjustments

```text
Draft → Approved → Posted
Draft / Approved → Cancelled
```

Draft items record the count, original on-hand quantity and balance row version. Editing can change the reason/counts and add/remove items. Existing items retain their original count snapshot.

Posting requires approval, a current adjustment version and unchanged balance snapshots. Counts cannot fall below reservations. Balance changes, `Adjustment` movements and the final status save together. Zero-delta items need no movement. Posted adjustments cannot be edited or cancelled. Stale counts require cancelling the adjustment and creating a fresh count.

### Branch transfers

```text
Draft → Requested → Approved → Dispatched → PartiallyReceived → Received
                                          └────────────────→ Received
Draft / Requested / Approved → Cancelled
```

Branches must be distinct and belong to the same tenant. Draft/request/approval do not move stock. Dispatch checks availability, deducts source on-hand and appends `TransferOut`; reservations remain unchanged.

Each receipt adds only the newly received quantity at the destination and appends `TransferIn`. Cumulative receipts cannot exceed the transferred quantity. Receipt retries reuse an `IdempotencyKey`. Cancellation after dispatch is rejected; a goods-resolution workflow is not yet implemented.

**API boundary:** dispatch and receipt commands/handlers are implemented and tested through MediatR, but the current transfer controller does not expose dispatch/receive endpoints.

### Customer returns

Sales supplies the return reference, quantities and restock flags. Inventory validates every item, increases on-hand only for restockable items and appends `Return` movements. Reservations are unchanged. Non-restockable goods create neither balance changes nor movements. Refunds remain owned by Sales.

A completed restock is identified by its return reference; movement idempotency keys also protect duplicate item writes. An entirely non-restockable request writes no processing record. The current integration is an HTTP command, not a Sales-event consumer.

### Low-stock alerts

After on-hand or threshold changes, the unit of work evaluates affected balances before saving:

1. On-hand **at or below** threshold: create an active alert if none exists and stage `LowStockDetected` in the outbox.
2. Still low: refresh the alert without staging another notification event.
3. On-hand **above** threshold: resolve the active alert.
4. A later drop starts a new episode with a new alert/event ID.

Alerts are scoped by tenant, branch, product and variant. Reserved-only changes do not trigger evaluation. Threshold zero still alerts when on-hand reaches zero.

## Local setup

### Prerequisites

- .NET 10 SDK.
- SQL Server and a development connection with migration permissions.
- Branch/Catalog schemas and data matching the read contracts above.
- An Identity service issuing tokens accepted by the configured OpenIddict issuer.
- RabbitMQ for publishing low-stock events; Docker is optional for running the local broker.

Run these PowerShell commands from the repository root.

### 1. Restore and configure

```powershell
dotnet restore .\Pos-InventoryService.slnx
dotnet build .\Pos-InventoryService.slnx
```

The API already has a user-secrets ID. Set local values without committing credentials:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=OperationsDb;Integrated Security=true;Encrypt=false;TrustServerCertificate=true" --project .\Pos.InventoryService.WebApi
dotnet user-secrets set "Services:Identity:Issuer" "https://localhost:YOUR_IDENTITY_PORT/" --project .\Pos.InventoryService.WebApi
dotnet user-secrets set "RabbitMqOptions:UserName" "posdev" --project .\Pos.InventoryService.WebApi
dotnet user-secrets set "RabbitMqOptions:Password" "YOUR_LOCAL_RABBITMQ_PASSWORD" --project .\Pos.InventoryService.WebApi
```

Replace the server/database, Identity URL and password with your values. The issuer must match the token issuer and be reachable for discovery/signing-key retrieval. The configured database must contain the Branch/Catalog contracts.

Merge these non-secret settings into `appsettings.Development.json`:

```json
{
  "StockReservationOptions": {
    "ExpiryMinutes": 15
  },
  "StockReservationExpirationOptions": {
    "CheckIntervalSeconds": 30,
    "BatchSize": 100
  },
  "RabbitMqOptions": {
    "HostName": "localhost",
    "Port": 5672,
    "VirtualHost": "pos-development",
    "ExchangeName": "inventory.events"
  },
  "OutboxPublisherOptions": {
    "PollingIntervalSeconds": 5,
    "BatchSize": 20,
    "PublishTimeoutSeconds": 10
  }
}
```

Use positive intervals, timeouts and batch sizes. Environment variables can override nested keys using double underscores, such as `ConnectionStrings__DefaultConnection`.

### 2. Start RabbitMQ

For a new local broker, choose a password matching the API configuration:

```powershell
$env:RABBITMQ_DEFAULT_PASS = "YOUR_LOCAL_RABBITMQ_PASSWORD"
docker run -d --name pos-rabbitmq --hostname pos-rabbitmq -p 127.0.0.1:5672:5672 -p 127.0.0.1:15672:15672 -e RABBITMQ_DEFAULT_USER=posdev -e RABBITMQ_DEFAULT_PASS -e RABBITMQ_DEFAULT_VHOST=pos-development -v pos-rabbitmq-data:/var/lib/rabbitmq rabbitmq:4-management
Remove-Item Env:RABBITMQ_DEFAULT_PASS
```

If the container already exists, use `docker start pos-rabbitmq`. Default user/vhost variables initialize a new broker; changing them does not reconfigure an existing data volume.

Open the [management UI](http://localhost:15672) and sign in with the configured credentials. AMQP uses port `5672`.

### 3. Apply Inventory migrations

Install the matching EF CLI if not already available:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.12
```

Apply migrations to the configured development database:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update --project .\Pos.InventoryService.Infrastructure.Persistence --startup-project .\Pos.InventoryService.WebApi --context ApplicationDbContext
```

Startup does not automatically apply migrations or seed business data. Provision Branch/Catalog data separately before initializing inventory.

### 4. Start the API

```powershell
dotnet dev-certs https --trust
dotnet run --project .\Pos.InventoryService.WebApi --launch-profile https
```

Open [Swagger](https://localhost:7082/swagger/index.html). The HTTPS profile uses `https://localhost:7082` and `http://localhost:5029`; HTTPS redirection is enabled. Swagger is enabled only in Development.

Select **Authorize** and paste the access token without the `Bearer` prefix. Other clients send `Authorization: Bearer <token>`.

## Authentication and authorization

Controllers require authentication and a valid non-empty `tenant_id` GUID. Handlers obtain the tenant from `ICurrentUserService`, not the request body. Operations recording an actor also require a GUID user identifier (`sub`, with name-identifier fallback).

Current policies use roles:

| Operation | Allowed roles |
|---|---|
| View balances/history/details/alerts | `TenantOwner`, `Admin`, `Cashier`, `InventoryStaff` |
| Opening stock, draft adjustments, transfers, receipt policy, thresholds | `TenantOwner`, `Admin`, `InventoryStaff` |
| Approve, post adjustments, cancel adjustments/transfers | `TenantOwner`, `Admin` |
| Create/consume/release reservations, restock returns | `TenantOwner`, `Admin`, `Cashier` |

Fine-grained permission claims are not required by the currently registered policies.

## HTTP endpoints

Paths are relative to `/api/v1`. Swagger describes request/response schemas.

| Method | Path | Operation |
|---|---|---|
| GET | `/StockBalances` | Filtered, paginated balances |
| GET | `/StockBalances/availability` | One branch/product/variant balance |
| POST | `/StockBalances/availability/batch` | Availability for requested item pairs |
| POST | `/StockBalances/opening-stock` | Initialize an item |
| PUT | `/StockBalances/threshold` | Update threshold using its row version |
| GET | `/StockMovements` | Filtered, paginated history |
| POST | `/StockAdjustments` | Create draft |
| PUT | `/StockAdjustments` | Update draft |
| GET | `/StockAdjustments/{adjustmentId}` | Details and items |
| POST | `/StockAdjustments/approve` | Approve draft |
| POST | `/StockAdjustments/cancel` | Cancel before posting |
| POST | `/StockAdjustments/post` | Apply approved count |
| POST | `/StockReservations` | Reserve sale items |
| POST | `/StockReservations/consume` | Complete reserved sale |
| POST | `/StockReservations/release` | Release hold |
| POST | `/StockReturns` | Restock eligible returned goods |
| POST | `/StockTransfers` | Create transfer draft |
| PUT | `/StockTransfers` | Update draft |
| GET | `/StockTransfers` | Filtered, paginated transfers |
| GET | `/StockTransfers/{transferId}` | Details and items |
| POST | `/StockTransfers/request` | Submit draft |
| POST | `/StockTransfers/approve` | Approve request |
| POST | `/StockTransfers/cancel` | Cancel before dispatch |
| GET | `/LowStockAlerts` | Filtered, paginated alerts |
| GET | `/LowStockAlerts/{id}` | Alert details |

List queries use nested parameter names:

```text
GET /api/v1/StockBalances?Parameter.Filter.BranchId=<branchId>&Parameter.Filter.LowStockOnly=true&Parameter.PageNumber=1&Parameter.PageSize=20

GET /api/v1/StockMovements?Parameter.Filter.BranchId=<branchId>&Parameter.Filter.FromUtc=2026-01-01T00:00:00Z&Parameter.Filter.ToUtcExclusive=2027-01-01T00:00:00Z
```

Movement type is optional. History's date interval includes `FromUtc` and excludes `ToUtcExclusive`. Ordering includes an ID tie-breaker for deterministic pages. History and alerts have no editing/deletion endpoints.

For commands requiring `RowVersion`, send the latest read's value. JSON represents `byte[]` as a Base64 string. Reload after a conflict rather than inventing a token. Middleware maps validation errors to 400, unauthorized-access exceptions to 403 and concurrency/duplicate-write exceptions to 409; controllers also map failed results according to the operation.

## Background processing and messaging

Both workers start and stop with the API host:

- **Reservation expiration:** query overdue active reservations, then process each in a fresh DI scope. Free reserved quantities and mark expired atomically.
- **Outbox publication:** query unpublished IDs in occurrence/ID order, then dispatch each in a fresh scope. Set `PublishedAt` only after successful publication. Failures remain pending for later polls.

```text
Stock/threshold command
    → evaluate low stock
    → save balance + alert + outbox in one database transaction
    → outbox worker
    → RabbitMQ publisher confirmation
    → mark outbox message published
```

The publisher reuses a connection/channel, serializes channel access, sends persistent JSON with `mandatory: true`, and enables publisher confirmations. It currently supports:

| Event | Exchange | Routing key |
|---|---|---|
| `LowStockDetected` | `inventory.events` (configurable, durable topic) | `inventory.low-stock.detected.v1` |

Payload: `EventId`, `TenantId`, `AlertId`, `BranchId`, `ProductId`, `ProductVariantId`, `QuantityOnHand`, `Threshold`, `DetectedAt`. RabbitMQ `MessageId` is the event ID.

The publisher declares and binds a durable learning queue, `inventory.low-stock.test`. Queue ownership should move to the receiving service/deployment when a real consumer is added. There is no notification consumer or inbox implementation here yet.

Delivery is **at least once**: publication may succeed while saving `PublishedAt` fails, so a retry can publish the same event again. Consumers must deduplicate by event ID. Staging one event per low-stock episode prevents repeated business alerts, but does not eliminate duplicate transport deliveries.

The outbox worker has no distributed claim/lease, retry backoff or dead-letter mechanism. Multiple API instances may publish the same pending message; permanently failing old messages can occupy a batch. Account for these limitations before scaling the publisher.

## Tests

Run the Catalog-style InMemory suite:

```powershell
dotnet test .\Pos.InventoryService.IntegrationTests
```

Run all tests against SQL Server from the same PowerShell session:

```powershell
$env:INVENTORY_TEST_SQL_CONNECTION = "Server=.;Integrated Security=true;Encrypt=false;TrustServerCertificate=true"
dotnet test .\Pos.InventoryService.IntegrationTests
Remove-Item Env:INVENTORY_TEST_SQL_CONNECTION
```

Use your SQL instance name. The account needs create/drop database permissions. Tests replace the database name with `InventoryIntegrationTests_<random GUID>`, seed isolated data and delete that database afterward. They never use the application database. SQL mode never silently falls back to InMemory.

To run only database-guarantee tests while the variable is set:

```powershell
dotnet test .\Pos.InventoryService.IntegrationTests --filter "Category=SqlServer"
```

Coverage includes tenant/role boundaries, validation, calculations, lifecycle transitions, duplicate requests, stale counts, partial receipts, alerts/outbox, HTTP routes, and SQL uniqueness/concurrency/rollback. Tests substitute token verification and RabbitMQ; live Identity/broker behavior and migration upgrades are not covered.

**Reported SQL Server run on 2026-09-26: 126 passed, 0 failed, 0 skipped.** The default InMemory run passes 119 and skips seven SQL-only tests. See [the integration-test guide](Pos.InventoryService.IntegrationTests/README.md) for fixture details and the coverage matrix.

## Troubleshooting

| Symptom | Check |
|---|---|
| Configuration fails during startup | JSON structure in API appsettings files |
| Authentication fails | Issuer, discovery connectivity, token validity and development HTTPS certificates |
| 403 response | Tenant claim and endpoint role |
| Item validation rejects a request | Branch/Catalog data, tenant ownership, active status, tracking, variant and unit rules |
| Conflict on update | Reload the current row version; stale adjustment counts need a fresh adjustment |
| Outbox remains pending | Broker credentials/vhost, connectivity, worker logs and binding |
| SQL tests are skipped | Set `INVENTORY_TEST_SQL_CONNECTION` in the terminal launching the tests |
| SQL tests fail with SSPI errors | Use an authenticated developer terminal and verify Windows access to the instance |
