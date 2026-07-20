using AFMS.Application;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Application.Validation;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;
using AFMS.Infrastructure;
using AFMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFMS.Tests.Infrastructure;

public sealed class Phase3StockIntegrationTests
{
    [Fact]
    public async Task Finalization_GeneratesBookletsMovements_AndSerialSearchFindsContainingBooklet()
    {
        await using var fixture = await StockFixture.CreateAsync();
        var receiving = fixture.Scope.ServiceProvider.GetRequiredService<IStockReceivingService>();
        var inventory = fixture.Scope.ServiceProvider.GetRequiredService<IInventoryService>();

        var draft = await receiving.SaveDraftAsync(fixture.ValidRequest(100001, 100500, 10, 50));
        Assert.Matches("^RCV-2026-[0-9]{6}$", draft.ReferenceNumber);
        await receiving.SubmitAsync(draft.Id);
        await receiving.FinalizeAsync(draft.Id);

        var context = fixture.Scope.ServiceProvider.GetRequiredService<AfmsDbContext>();
        Assert.Equal(10, await context.AccountableFormBooklets.CountAsync());
        Assert.Equal(10, await context.StockMovements.CountAsync(item => item.MovementType == StockMovementType.Received));
        Assert.Equal(TransactionStatus.Finalized, (await context.StockReceipts.SingleAsync()).Status);

        var match = Assert.Single(await inventory.SearchAsync(new InventoryFilter(SerialNumber: 100025)));
        Assert.Equal(100001, match.BeginningSerial);
        Assert.Equal(100050, match.EndingSerial);
        Assert.Equal(50, match.RemainingQuantity);
    }

    [Fact]
    public async Task DuplicateOrOverlappingReceivingRange_IsRejected()
    {
        await using var fixture = await StockFixture.CreateAsync();
        var receiving = fixture.Scope.ServiceProvider.GetRequiredService<IStockReceivingService>();
        var first = await receiving.SaveDraftAsync(fixture.ValidRequest(200001, 200100, 2, 50));
        await receiving.SubmitAsync(first.Id);
        await receiving.FinalizeAsync(first.Id);

        await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            receiving.SaveDraftAsync(fixture.ValidRequest(200050, 200149, 2, 50)));
        Assert.Equal(1, await fixture.Scope.ServiceProvider.GetRequiredService<AfmsDbContext>().StockReceipts.CountAsync());
    }

    [Fact]
    public async Task DatabaseTrigger_RejectsOverlappingRange_WhenApplicationValidationIsBypassed()
    {
        await using var fixture = await StockFixture.CreateAsync();
        var receiving = fixture.Scope.ServiceProvider.GetRequiredService<IStockReceivingService>();
        var first = await receiving.SaveDraftAsync(fixture.ValidRequest(210001, 210100, 2, 50));
        await receiving.SubmitAsync(first.Id);
        await receiving.FinalizeAsync(first.Id);

        var context = fixture.Scope.ServiceProvider.GetRequiredService<AfmsDbContext>();
        var userId = await context.Users.Select(user => user.Id).SingleAsync();
        var bypassedReceipt = new StockReceipt
        {
            ReferenceNumber = "RCV-2026-BYPASS",
            ReceivedDate = new DateOnly(2026, 7, 17),
            AccountableFormTypeId = fixture.FormTypeId,
            EncodedByUserId = userId,
            SeriesYear = 2026,
            StorageLocation = "Treasury Vault"
        };
        bypassedReceipt.Items.Add(new StockReceiptItem
        {
            AccountableFormTypeId = fixture.FormTypeId,
            BeginningSerial = 210050,
            EndingSerial = 210099,
            Quantity = 50,
            NumberOfBooklets = 1,
            ReceiptsPerBooklet = 50
        });
        context.Add(bypassedReceipt);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("Overlapping stock receipt serial range", exception.InnerException?.Message ?? exception.Message);
        context.ChangeTracker.Clear();
        Assert.Equal(1, await context.StockReceipts.CountAsync());
    }

    [Fact]
    public async Task FailedFinalization_RollsBackBookletsMovementsAndStatus()
    {
        await using var fixture = await StockFixture.CreateAsync();
        var receiving = fixture.Scope.ServiceProvider.GetRequiredService<IStockReceivingService>();
        var draft = await receiving.SaveDraftAsync(fixture.ValidRequest(300001, 300100, 2, 50));
        await receiving.SubmitAsync(draft.Id);
        var context = fixture.Scope.ServiceProvider.GetRequiredService<AfmsDbContext>();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER Phase3_Test_ForcedFailure
            BEFORE INSERT ON StockMovements
            WHEN NEW.EndingSerial = 300100
            BEGIN
                SELECT RAISE(ABORT, 'forced finalization failure');
            END;
            """);

        await Assert.ThrowsAnyAsync<Exception>(() => receiving.FinalizeAsync(draft.Id));

        context.ChangeTracker.Clear();
        Assert.Equal(TransactionStatus.Submitted, (await context.StockReceipts.SingleAsync()).Status);
        Assert.Empty(await context.AccountableFormBooklets.ToListAsync());
        Assert.Empty(await context.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task ReferenceGeneration_UsesIndependentAtomicSequences()
    {
        await using var fixture = await StockFixture.CreateAsync();
        var tasks = Enumerable.Range(0, 8).Select(async _ =>
        {
            using var scope = fixture.Provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IReferenceNumberService>().GenerateAsync("RCV", 2027);
        });
        var references = await Task.WhenAll(tasks);
        Assert.Equal(8, references.Distinct().Count());
        Assert.Contains("RCV-2027-000001", references);
        Assert.Contains("RCV-2027-000008", references);

        using var verificationScope = fixture.Provider.CreateScope();
        Assert.Equal("ISS-2027-000001", await verificationScope.ServiceProvider.GetRequiredService<IReferenceNumberService>().GenerateAsync("ISS", 2027));
    }

    private sealed class StockFixture : IAsyncDisposable
    {
        private readonly string _databasePath;
        public ServiceProvider Provider { get; }
        public IServiceScope Scope { get; }
        public Guid FormTypeId { get; }

        private StockFixture(string databasePath, ServiceProvider provider, IServiceScope scope, Guid formTypeId)
        { _databasePath = databasePath; Provider = provider; Scope = scope; FormTypeId = formTypeId; }

        public static async Task<StockFixture> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"afms-phase3-{Guid.NewGuid():N}.db");
            var services = new ServiceCollection();
            services.AddLogging(); services.AddApplication(); services.AddInfrastructure($"Data Source={path};Pooling=False");
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
            var context = scope.ServiceProvider.GetRequiredService<AfmsDbContext>();
            var user = new User { Username = "phase3.admin", DisplayName = "Phase 3 Administrator", PasswordHash = "test-only-hash" };
            var form = new AccountableFormType { Code = "AF51-TEST", Name = "AF 51 Test", DefaultReceiptsPerBooklet = 50 };
            context.AddRange(user, form); await context.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<ICurrentUserSession>().Begin(new CurrentUser(user.Id, user.Username, user.DisplayName,
                new HashSet<string> { "Administrator" }, Enum.GetValues<SystemPermission>().ToHashSet(), false));
            return new StockFixture(path, provider, scope, form.Id);
        }

        public SaveStockReceiptDraftRequest ValidRequest(long beginning, long ending, int books, int perBook) =>
            new(null, new DateOnly(2026, 7, 16), FormTypeId, "Bureau of Local Government Finance", "DR-001", 2026,
                "Treasury Vault", "Municipal Treasurer", "Integration test", [new(books, perBook, beginning, ending)]);

        public async ValueTask DisposeAsync()
        {
            Scope.Dispose(); await Provider.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_databasePath)) File.Delete(_databasePath);
        }
    }
}
