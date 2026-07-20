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

public sealed class Phase4IssuanceIntegrationTests
{
    [Fact]
    public async Task ReceivingToIssuance_FinalizesAndCreatesCollectorLedgerBalance()
    {
        await using var fixture = await Fixture.CreateAsync(410001);
        var issuance = fixture.Service<IIssuanceService>();
        var draft = await fixture.CreateIssuanceAsync([fixture.BookletIds[0]]);
        await issuance.SubmitAsync(draft.Id); await fixture.ApproveAsync(() => issuance.ApproveAsync(draft.Id)); await issuance.FinalizeAsync(draft.Id);

        var context = fixture.Service<AfmsDbContext>(); context.ChangeTracker.Clear();
        var booklet = await context.AccountableFormBooklets.SingleAsync(item => item.Id == fixture.BookletIds[0]);
        Assert.Equal(AccountableFormStatus.Issued, booklet.Status);
        Assert.Equal(fixture.CollectorId, booklet.CurrentCollectorId);
        Assert.Equal(fixture.BarangayId, booklet.CurrentBarangayId);
        Assert.Single(await context.StockMovements.Where(item => item.MovementType == StockMovementType.Issued).ToListAsync());

        var ledger = await fixture.Service<ICollectorAccountabilityService>().GetLedgerAsync(new(fixture.CollectorId));
        var entry = Assert.Single(ledger);
        Assert.Equal(AccountabilityTransactionType.Issuance, entry.TransactionType);
        Assert.Equal(50, entry.QuantityIn);
        Assert.Equal(50, entry.RunningBalance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => issuance.FinalizeAsync(draft.Id));
        Assert.Equal(1, await context.StockMovements.CountAsync(item => item.MovementType == StockMovementType.Issued));
    }

    [Fact]
    public async Task RestartBeforeIssuance_PreservesRangeOwnershipAndAllowsUsage()
    {
        await using var fixture = await Fixture.CreateAsync(415001);
        await fixture.RestartAsync();

        var issuance = fixture.Service<IIssuanceService>();
        var draft = await fixture.CreateIssuanceAsync([fixture.BookletIds[0]]);
        await issuance.SubmitAsync(draft.Id);
        await fixture.ApproveAsync(() => issuance.ApproveAsync(draft.Id));
        await issuance.FinalizeAsync(draft.Id);

        var operations = fixture.Service<IOperationalTransactionService>();
        var owned = await operations.ListOwnedRangesAsync(fixture.CollectorId, fixture.FormTypeId);
        Assert.Contains(owned, range => range.BookletId == fixture.BookletIds[0] && range.BeginningSerial == 415001 && range.EndingSerial == 415050);

        var date = new DateOnly(2026, 7, 16);
        var usage = await operations.SaveUsageDraftAsync(new(null, fixture.CollectorId, fixture.FormTypeId, date, date, date,
            null, "RCD-415", null, [new(fixture.BookletIds[0], 415001, 415005)]));
        await operations.FinalizeUsageAsync(usage.Id);

        var context = fixture.Service<AfmsDbContext>();
        context.ChangeTracker.Clear();
        Assert.Equal(5, await context.StockMovements.Where(x => x.MovementType == StockMovementType.Used).SumAsync(x => x.Quantity));
        Assert.Contains(await context.AccountabilityRanges.Where(x => x.IsActive).ToListAsync(),
            range => range.CurrentCollectorId == fixture.CollectorId && range.BeginningSerial == 415006 && range.EndingSerial == 415050);
    }

    [Fact]
    public async Task DraftReservation_PreventsDuplicateIssuance()
    {
        await using var fixture = await Fixture.CreateAsync(420001);
        await fixture.CreateIssuanceAsync([fixture.BookletIds[0]]);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => fixture.CreateIssuanceAsync([fixture.BookletIds[0]]));
        Assert.Equal(1, await fixture.Service<AfmsDbContext>().Issuances.CountAsync());
    }

    [Fact]
    public async Task Issuance_SelfApprovalIsRejectedBySeparationOfDuties()
    {
        await using var fixture = await Fixture.CreateAsync(425001);
        var issuance = fixture.Service<IIssuanceService>();
        var draft = await fixture.CreateIssuanceAsync([fixture.BookletIds[0]]);
        await issuance.SubmitAsync(draft.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => issuance.ApproveAsync(draft.Id));
        await fixture.ApproveAsync(() => issuance.ApproveAsync(draft.Id));
    }

    [Fact]
    public async Task InactiveCollector_IsRejected()
    {
        await using var fixture = await Fixture.CreateAsync(430001);
        var context = fixture.Service<AfmsDbContext>();
        (await context.Collectors.SingleAsync(item => item.Id == fixture.CollectorId)).IsActive = false; await context.SaveChangesAsync();
        await Assert.ThrowsAsync<ApplicationValidationException>(() => fixture.CreateIssuanceAsync([fixture.BookletIds[0]]));
    }

    [Fact]
    public async Task FailedIssuanceFinalization_RollsBackEveryBookletMovementAndStatus()
    {
        await using var fixture = await Fixture.CreateAsync(440001);
        var issuance = fixture.Service<IIssuanceService>();
        var draft = await fixture.CreateIssuanceAsync(fixture.BookletIds);
        await issuance.SubmitAsync(draft.Id); await fixture.ApproveAsync(() => issuance.ApproveAsync(draft.Id));
        var context = fixture.Service<AfmsDbContext>();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER Phase4_Test_ForcedFailure
            BEFORE INSERT ON StockMovements
            WHEN NEW.MovementType = 2 AND NEW.EndingSerial = 440100
            BEGIN SELECT RAISE(ABORT, 'forced issuance failure'); END;
            """);

        await Assert.ThrowsAnyAsync<Exception>(() => issuance.FinalizeAsync(draft.Id));
        context.ChangeTracker.Clear();
        Assert.Equal(TransactionStatus.Approved, (await context.Issuances.SingleAsync()).Status);
        Assert.All(await context.AccountableFormBooklets.ToListAsync(), item => Assert.Equal(AccountableFormStatus.Available, item.Status));
        Assert.Empty(await context.StockMovements.Where(item => item.MovementType == StockMovementType.Issued).ToListAsync());
        Assert.All(await context.IssuanceItems.ToListAsync(), item => Assert.True(item.IsReservationActive));
    }

    [Fact]
    public async Task CustodianAndViewerPermissions_AreEnforced()
    {
        await using var fixture = await Fixture.CreateAsync(450001);
        var session = fixture.Service<ICurrentUserSession>();
        session.Begin(new CurrentUser(fixture.UserId, "viewer", "Viewer", new HashSet<string> { "Auditor or Viewer" },
            new HashSet<SystemPermission> { SystemPermission.ViewIssuances, SystemPermission.ViewCollectorAccountability }, false));
        Assert.Empty(await fixture.Service<IIssuanceService>().ListAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.CreateIssuanceAsync([fixture.BookletIds[0]]));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _path;
        public ServiceProvider Provider { get; private set; }
        public IServiceScope Scope { get; private set; }
        public Guid UserId { get; }
        public Guid SupervisorId { get; }
        public Guid CollectorId { get; }
        public Guid BarangayId { get; }
        public Guid FormTypeId { get; }
        public IReadOnlyList<Guid> BookletIds { get; }
        private Fixture(string path, ServiceProvider provider, IServiceScope scope, Guid userId, Guid supervisorId, Guid collectorId, Guid barangayId, Guid formTypeId, IReadOnlyList<Guid> bookletIds)
        { _path = path; Provider = provider; Scope = scope; UserId = userId; SupervisorId = supervisorId; CollectorId = collectorId; BarangayId = barangayId; FormTypeId = formTypeId; BookletIds = bookletIds; }
        public T Service<T>() where T : notnull => Scope.ServiceProvider.GetRequiredService<T>();

        public static async Task<Fixture> CreateAsync(long beginning)
        {
            var path = Path.Combine(Path.GetTempPath(), $"afms-phase4-{Guid.NewGuid():N}.db");
            var services = new ServiceCollection(); services.AddLogging(); services.AddApplication(); services.AddInfrastructure($"Data Source={path};Pooling=False");
            var provider = services.BuildServiceProvider(); var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
            var context = scope.ServiceProvider.GetRequiredService<AfmsDbContext>();
            var user = new User { Username = "phase4.admin", DisplayName = "Phase 4 Administrator", PasswordHash = "test-only" };
            var supervisor = new User { Username = $"phase4.supervisor.{beginning}", DisplayName = "Phase 4 Supervisor", PasswordHash = "test-only" };
            var form = new AccountableFormType { Code = $"AF51-{beginning}", Name = "LOCAL", DefaultReceiptsPerBooklet = 50 };
            var barangay = new Barangay { Code = $"B-{beginning}", Name = $"Barangay {beginning}", Municipality = "Dipaculao", Province = "Aurora" };
            var collector = new Collector { Code = $"C-{beginning}", FullName = $"Collector {beginning}", Barangay = barangay };
            context.AddRange(user, supervisor, form, barangay, collector); await context.SaveChangesAsync();
            var session = scope.ServiceProvider.GetRequiredService<ICurrentUserSession>();
            session.Begin(new CurrentUser(user.Id, user.Username, user.DisplayName, new HashSet<string> { "Administrator" }, Enum.GetValues<SystemPermission>().ToHashSet(), false));
            var receiving = scope.ServiceProvider.GetRequiredService<IStockReceivingService>();
            var receipt = await receiving.SaveDraftAsync(new(null, new DateOnly(2026, 7, 16), form.Id, "BLGF", "DR-4", 2026, "Treasury Vault", "Treasurer", null,
                [new(2, 50, beginning, beginning + 99)]));
            await receiving.SubmitAsync(receipt.Id); await receiving.FinalizeAsync(receipt.Id);
            context.ChangeTracker.Clear();
            var bookletIds = await context.AccountableFormBooklets.OrderBy(item => item.BeginningSerial).Select(item => item.Id).ToListAsync();
            return new Fixture(path, provider, scope, user.Id, supervisor.Id, collector.Id, barangay.Id, form.Id, bookletIds);
        }

        public async Task RestartAsync()
        {
            Scope.Dispose();
            await Provider.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            var services = new ServiceCollection();
            services.AddLogging(); services.AddApplication(); services.AddInfrastructure($"Data Source={_path};Pooling=False");
            Provider = services.BuildServiceProvider(); Scope = Provider.CreateScope();
            await Scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
            Scope.ServiceProvider.GetRequiredService<ICurrentUserSession>().Begin(new CurrentUser(UserId, "phase4.admin", "Phase 4 Administrator",
                new HashSet<string> { "Administrator" }, Enum.GetValues<SystemPermission>().ToHashSet(), false));
        }

        public Task<IssuanceDto> CreateIssuanceAsync(IReadOnlyList<Guid> bookletIds) => Service<IIssuanceService>().SaveDraftAsync(new(
            null, new DateOnly(2026, 7, 16), CollectorId, BarangayId, null, "Revenue collection", "Treasury Custodian", "Collector", null, bookletIds));

        public async Task ApproveAsync(Func<Task> action)
        {
            var session = Service<ICurrentUserSession>();
            session.Begin(new CurrentUser(SupervisorId, "phase4.supervisor", "Phase 4 Supervisor", new HashSet<string> { "Supervisor" }, Enum.GetValues<SystemPermission>().ToHashSet(), false));
            try { await action(); }
            finally { session.Begin(new CurrentUser(UserId, "phase4.admin", "Phase 4 Administrator", new HashSet<string> { "Administrator" }, Enum.GetValues<SystemPermission>().ToHashSet(), false)); }
        }

        public async ValueTask DisposeAsync()
        {
            Scope.Dispose(); await Provider.DisposeAsync(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (File.Exists(_path)) File.Delete(_path);
        }
    }
}
