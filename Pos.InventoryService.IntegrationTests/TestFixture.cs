using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.InventoryService.Application;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Publisher;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Domain.Models;
using Pos.InventoryService.Infrastructure.Persistence;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;
using Pos.InventoryService.Infrastructure.Persistence.ReadModels;
using Pos.InventoryService.Infrastructure.Shared.Services;

namespace Pos.InventoryService.IntegrationTests;

// Like Catalog: real MediatR handlers, validators, repositories and unit of work.
// Each test owns its database; every SendAsync creates a fresh request scope.
public sealed class TestFixture : IDisposable
{
    public ServiceProvider Services { get; }
    public IReadOnlyList<ServiceDescriptor> Registrations { get; }
    public FakeCurrentUserService CurrentUser { get; } = new();
    public RecordingEventPublisher Publisher { get; } = new();
    public Guid TenantId { get; } = Guid.NewGuid();
    public Guid BranchId { get; } = Guid.NewGuid();
    public Guid OtherBranchId { get; } = Guid.NewGuid();
    public Guid ProductId { get; } = Guid.NewGuid();
    public Guid DecimalProductId { get; } = Guid.NewGuid();
    public Guid VariantProductId { get; } = Guid.NewGuid();
    public Guid VariantId { get; } = Guid.NewGuid();
    public Guid OtherVariantId { get; } = Guid.NewGuid();
    public Guid NonTrackedProductId { get; } = Guid.NewGuid();
    public Guid UnitId { get; } = Guid.NewGuid();
    public Guid DecimalUnitId { get; } = Guid.NewGuid();
    public bool IsSqlServer { get; }

    public TestFixture()
    {
        var name = "InventoryIntegrationTests_" + Guid.NewGuid().ToString("N");
        var server = Environment.GetEnvironmentVariable("INVENTORY_TEST_SQL_CONNECTION");
        IsSqlServer = !string.IsNullOrWhiteSpace(server);
        var configurationValues = new Dictionary<string, string?>();
        if (IsSqlServer)
        {
            // Never use or delete the database named in the supplied connection string.
            var connection = new SqlConnectionStringBuilder(server)
            {
                InitialCatalog = name, AttachDBFilename = "", Pooling = false
            };
            configurationValues["ConnectionStrings:DefaultConnection"] = connection.ConnectionString;
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configurationValues).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationLayer(configuration);
        services.AddPersistenceServices(configuration);
        if (!IsSqlServer)
        {
            // Override the context registration only. Repositories and services remain real.
            services.AddScoped<ApplicationDbContext>(_ => new InMemoryTestContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(name).Options));
        }
        CurrentUser.TenantId = TenantId;
        services.AddSingleton<ICurrentUserService>(CurrentUser);
        services.AddSingleton<IEventPublisher>(Publisher);
        services.AddScoped<IStockReservationReleaseService, StockReservationReleaseService>();
        services.AddScoped<OutboxDispatcher>();
        Registrations = services.ToList();
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        if (IsSqlServer)
        {
            db.Database.ExecuteSqlRaw("""
                CREATE SCHEMA branch;
                """);
            db.Database.ExecuteSqlRaw("CREATE SCHEMA Catalog;");
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE branch.Branches (Id uniqueidentifier PRIMARY KEY, TenantId uniqueidentifier NOT NULL, Status nvarchar(30) NOT NULL);
                CREATE TABLE Catalog.Units (Id uniqueidentifier PRIMARY KEY, TenantId uniqueidentifier NULL, IsDecimalAllowed bit NOT NULL);
                CREATE TABLE Catalog.Products (Id uniqueidentifier PRIMARY KEY, TenantId uniqueidentifier NOT NULL, UnitId uniqueidentifier NOT NULL, Status nvarchar(30) NOT NULL, TrackInventory bit NOT NULL);
                CREATE TABLE Catalog.ProductVariants (Id uniqueidentifier PRIMARY KEY, TenantId uniqueidentifier NOT NULL, ProductId uniqueidentifier NOT NULL, Status nvarchar(30) NOT NULL);
                """);
            db.Database.ExecuteSqlInterpolated($"INSERT INTO branch.Branches VALUES ({BranchId},{TenantId},'Active'),({OtherBranchId},{TenantId},'Active')");
            db.Database.ExecuteSqlInterpolated($"INSERT INTO Catalog.Units VALUES ({UnitId},{TenantId},0),({DecimalUnitId},{TenantId},1)");
            foreach (var p in new[] { ProductId, DecimalProductId, VariantProductId, NonTrackedProductId })
                db.Database.ExecuteSqlInterpolated($"INSERT INTO Catalog.Products VALUES ({p},{TenantId},{(p == DecimalProductId ? DecimalUnitId : UnitId)},'Active',{p != NonTrackedProductId})");
            db.Database.ExecuteSqlInterpolated($"INSERT INTO Catalog.ProductVariants VALUES ({VariantId},{TenantId},{VariantProductId},'Active'),({OtherVariantId},{TenantId},{VariantProductId},'Active')");
        }
        else
        {
            db.AddRange(new InventoryBranchReadModel { Id = BranchId, TenantId = TenantId, Status = "Active" },
                new InventoryBranchReadModel { Id = OtherBranchId, TenantId = TenantId, Status = "Active" });
            db.AddRange(new InventoryUnitReadModel { Id = UnitId, TenantId = TenantId },
                new InventoryUnitReadModel { Id = DecimalUnitId, TenantId = TenantId, IsDecimalAllowed = true });
            foreach (var p in new[] { ProductId, DecimalProductId, VariantProductId, NonTrackedProductId })
                db.Add(new InventoryProductReadModel { Id = p, TenantId = TenantId, UnitId = p == DecimalProductId ? DecimalUnitId : UnitId, Status = "Active", TrackInventory = p != NonTrackedProductId });
            db.AddRange(new InventoryVariantReadModel { Id = VariantId, TenantId = TenantId, ProductId = VariantProductId, Status = "Active" },
                new InventoryVariantReadModel { Id = OtherVariantId, TenantId = TenantId, ProductId = VariantProductId, Status = "Active" });
            db.SaveChanges();
        }
    }

    public async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    public async Task<T> ReadAsync<T>(Func<ApplicationDbContext, Task<T>> read)
    {
        await using var scope = Services.CreateAsyncScope();
        return await read(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    public async Task WriteAsync(Func<ApplicationDbContext, Task> change, bool evaluateAlerts = false)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await change(db);
        if (evaluateAlerts) await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        else await db.SaveChangesAsync();
    }

    public async Task<StockBalance> SeedBalanceAsync(decimal quantity = 100, decimal reserved = 0,
        Guid? product = null, Guid? variant = null, Guid? branch = null, Guid? tenant = null, decimal threshold = 5)
    {
        var balance = new StockBalance { Id = Guid.NewGuid(), TenantId = tenant ?? TenantId,
            BranchId = branch ?? BranchId, ProductId = product ?? ProductId, ProductVariantId = variant,
            QuantityOnHand = quantity, QuantityReserved = reserved, LowStockThreshold = threshold };
        await WriteAsync(db => { db.Add(balance); return Task.CompletedTask; });
        return balance;
    }

    public Task<StockBalance> BalanceAsync(Guid? product = null, Guid? branch = null, Guid? variant = null) =>
        ReadAsync(db => db.StockBalances.SingleAsync(x => x.TenantId == TenantId &&
            x.BranchId == (branch ?? BranchId) && x.ProductId == (product ?? ProductId) && x.ProductVariantId == variant));

    public void Dispose()
    {
        using (var scope = Services.CreateScope())
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureDeleted();
        Services.Dispose();
    }
}

public sealed class FakeCurrentUserService : ICurrentUserService
{
    public Guid? TenantId { get; set; }
    public string? UserId { get; set; } = Guid.NewGuid().ToString();
    public string? UserType => "tenant";
    public IReadOnlyList<string> Roles => ["TenantOwner"];
    public string? AccessToken => null;
}

public sealed class RecordingEventPublisher : IEventPublisher
{
    public List<(Guid Id, string Type, string Payload)> Messages { get; } = [];
    public bool Fail { get; set; }
    public Task PublishAsync(Guid eventId, string eventType, string payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Fail) throw new IOException("Simulated broker failure");
        Messages.Add((eventId, eventType, payload));
        return Task.CompletedTask;
    }
}

// In-memory mode is for handler behavior, NOT relational guarantees.
// Keys allow seeding the external read contracts. Tokens simulate version changes.
internal sealed class InMemoryTestContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<InventoryBranchReadModel>().HasKey(x => x.Id);
        modelBuilder.Entity<InventoryProductReadModel>().HasKey(x => x.Id);
        modelBuilder.Entity<InventoryVariantReadModel>().HasKey(x => x.Id);
        modelBuilder.Entity<InventoryUnitReadModel>().HasKey(x => x.Id);
        foreach (var type in modelBuilder.Model.GetEntityTypes())
            if (type.FindProperty("RowVersion") is not null)
                modelBuilder.Entity(type.ClrType).Property("RowVersion").ValueGeneratedNever();
    }
    private void StampVersions()
    {
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified))
            if (entry.Metadata.FindProperty("RowVersion") is not null)
                entry.Property("RowVersion").CurrentValue = Guid.NewGuid().ToByteArray()[..8];
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess) { StampVersions(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    { StampVersions(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
}

public abstract class InventoryTest : IDisposable
{
    protected TestFixture Fixture { get; } = new();
    public void Dispose() => Fixture.Dispose();
}

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INVENTORY_TEST_SQL_CONNECTION")))
            Skip = "Requires INVENTORY_TEST_SQL_CONNECTION: real SQL Server row versions, indexes and transactions.";
    }
}
