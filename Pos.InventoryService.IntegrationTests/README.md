# Inventory integration tests

This follows Catalog's xUnit + FluentAssertions + DI + MediatR test style. Tests use the real validation pipeline, handlers, repositories, item validation service, unit of work and low-stock evaluator. Every command gets a fresh DI scope, and assertions reload persisted data using a different context.

## Run

From the solution directory:

```powershell
dotnet test .\Pos.InventoryService.IntegrationTests\Pos.InventoryService.IntegrationTests.csproj
```

Or use Visual Studio **Test Explorer → Run All**. No running API, token, Catalog service, RabbitMQ or existing data is required.

For a faster run without the HTTP tests:

```powershell
dotnet test .\Pos.InventoryService.IntegrationTests --filter 'FullyQualifiedName!~ApiTests'
```

## Two database modes

**Default: EF InMemory**, matching Catalog. Each test gets an isolated database. The test-only context gives external read models keys so their data can be seeded, and simulates changing row-version tokens. This checks handler decisions, stale-version checks, query filters and whether failed commands leave persisted data unchanged. It does **not** prove SQL transactions, SQL query translation, decimal storage, foreign keys, unique indexes or SQL-generated row versions.

**SQL Server:** set a connection string for a development/test SQL Server where your account may create and drop databases:

```powershell
$env:INVENTORY_TEST_SQL_CONNECTION = 'Server=.;Integrated Security=true;Encrypt=false;TrustServerCertificate=true'
dotnet test .\Pos.InventoryService.IntegrationTests
Remove-Item Env:INVENTORY_TEST_SQL_CONNECTION
```

The fixture always replaces the database name with `InventoryIntegrationTests_<random GUID>`. It creates and deletes only that database. It does not read application connection strings or use the development database. Test tables provide only the Branch/Catalog columns that Inventory's read contracts need.

In SQL mode **all tests** use the real `ApplicationDbContext` and SQL provider. Additional `[SqlServerFact]` cases cover database guarantees. Without the environment variable, those cases show as **skipped**, not passed. To run just those:

```powershell
dotnet test .\Pos.InventoryService.IntegrationTests --filter 'Category=SqlServer'
```

Schema creation uses `EnsureCreated` from the current EF model. Migration upgrade paths are not tested. An interrupted SQL run may leave a database whose name starts with `InventoryIntegrationTests_`; the fixture normally removes it on disposal.

## Coverage

| Area | Cases |
|---|---|
| Opening stock | Balance + before/after movement, replay, changed replay, duplicate initialization, positive quantity/precision, whole/decimal units, variant selection |
| Ownership | Missing tenant, foreign tenant/branch/product/variant/unit, inactive branch/product/variant, global units, non-inventory products |
| Balance queries | Exact null/specific variant, batch pairs and deduplication, computed available quantity, branch/tenant isolation, low-stock filter, pagination |
| Adjustments | Draft snapshot/details, reason/count editing, add/remove items, preserve original count snapshot, approval, cancellation, posting once, no-op counts, immutable posted state, stale versions/counts, reservation floor, all-item rejection |
| Reservations | Server expiry, availability, duplicate sale, insufficient item rejects entire request, consumption once, sale history, release/expiry once, overdue selection, tenant boundaries, terminal status conflicts, failed multi-item consumption |
| Transfers | Distinct owned branches, draft replacement, request/approval rules, dispatch availability, source-only deduction, reserved stock preserved, pre-dispatch cancellation, post-dispatch rejection, partial/full receipts, cumulative limit, receipt replay, invalid receipt item rollback, queries/tenant boundaries |
| Returns | Restock flags, valid non-restockable goods produce no balance/movement, validate every item, group repeated items, create missing balance, return replay, overflow, tenant boundaries |
| Low stock | Equality activates, still-low refresh without duplicate event, recovery resolves, new episode reactivates, reserved-only changes ignored, separate branch/variant/tenant alerts, event snapshot, list/detail tenant isolation |
| Outbox | Oldest pending batch, published exclusion, broker failure leaves pending, retry preserves event ID/payload, successful publish marks sent, repeated dispatch skips sent/missing messages |
| HTTP | Every existing route rejects anonymous/unauthorized callers; agreed role policies; tenant claim requirement; versioned routing; model/pipeline validation; cross-tenant read; optional MovementType filter; Swagger generation; no history/alert mutation endpoints |
| SQL-only | Null-variant balance uniqueness, active-alert uniqueness, item idempotency uniqueness, atomic rollback including alert/outbox, competing final reservation transitions, concurrent opening replay, competing reservations and availability recheck on a fresh retry |

`ApiTests` starts an in-process TestServer with the real controllers, policies and middleware. It substitutes token verification with a **test-only** authentication handler; production `CurrentUserService` reads the resulting claims. It does not start the production host's background workers or test OpenIddict token validation. Dispatch and receipt are tested through MediatR because the current transfer controller does not expose those routes.

The default outbox tests replace RabbitMQ with a recording publisher. The opt-in consumer tests below use a real broker; they do not verify publisher recovery or outbox polling intervals. Reservation creation currently surfaces concurrency conflicts; its SQL test retries in a fresh request scope and verifies availability is rechecked. It does not claim there is an automatic internal retry loop.

## Regressions found while adding the suite

- Reservation creation used `GetByIdAsync` with a sale ID. It now uses a tenant-scoped reference-ID lookup so duplicate sales return the existing reservation.
- New adjustment/transfer items with assigned GUIDs were interpreted as existing rows when added to tracked drafts. Their EF configurations now specify `ValueGeneratedNever`, matching command-assigned IDs. Both draft-edit regressions are covered. EF reports no pending relational model changes, so these tracking fixes require no database migration.

## Execution notes

On 2026-09-26 the complete default suite finished with **119 passed, 7 skipped, 0 failed** (126 cases, approximately 82 seconds). SQL Server execution was attempted separately, but Windows authentication failed with `Cannot generate SSPI context`; SQL guarantees remain unverified in that environment. Run SQL mode from your normal developer terminal to verify those cases. No tests silently fall back to InMemory when SQL mode is explicitly requested.


## Inbox and RabbitMQ consumer tests

`InboxTests` uses the real event validator, return handler, restock service, repositories and dispatcher.
It covers event replay, eligible/non-restockable/untracked items, metadata and actor recording,
invalid JSON/IDs/quantities/conditions, and unrelated unique failures propagating for retry.
The SQL-only tests cover atomic rollback, overlapping deliveries, and a concurrent inbox insert.
The overlap test deliberately waits until both requests have staged their changes before allowing saves.

Run just this feature:

```powershell
dotnet test .\Pos.InventoryService.IntegrationTests --filter 'FullyQualifiedName~InboxTests|FullyQualifiedName~RabbitMqConsumerTests'
```

The three `RabbitMqConsumerTests` are skipped unless `INVENTORY_TEST_RABBITMQ_URI` is set.
Use a disposable test broker/vhost with permission to declare/delete exchanges and quorum queues:

```powershell
$env:INVENTORY_TEST_RABBITMQ_URI = 'amqp://guest:guest@localhost:5672/%2F'
dotnet test .\Pos.InventoryService.IntegrationTests --filter 'Category=RabbitMq'
Remove-Item Env:INVENTORY_TEST_RABBITMQ_URI
```

Each broker test starts the actual consumer worker and creates uniquely named `inventory.integration.*`
exchanges/queues, then deletes only its own resources. It verifies routing and duplicate delivery,
acknowledgement after processing, permanent rejection to the dead-letter queue, and transient redelivery.
Set `INVENTORY_TEST_SQL_CONNECTION` as well to use SQL Server for these broker tests; otherwise they use
InMemory and do not establish relational transaction guarantees. No application credentials are read.
Automatic reconnection, retry exhaustion and migration upgrades are not covered by these tests.

On 2026-10-17 the inbox/consumer subset completed with **11 passed, 6 skipped, 0 failed**.
The skipped cases were the three SQL Server guarantees and three RabbitMQ tests because their
environment variables were not configured. Compilation and default behaviour passed; real broker
and SQL guarantees still require the opt-in runs above.
