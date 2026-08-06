using AFMS.Core;
using AFMS.Core.Domain;
using AFMS.Core.Persistence;
using AFMS.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Tests;

/// <summary>
/// Spins up a throwaway SQLite database on disk with a signed-in user, so tests
/// exercise the same query translation, check constraints, and triggers the real
/// app does. An in-memory provider would hide all three.
/// </summary>
public sealed class InventoryFixture : IAsyncDisposable
{
    private readonly string _databasePath;

    private InventoryFixture(string databasePath, InventoryDbContext context, CurrentUser session)
    {
        _databasePath = databasePath;
        Context = context;
        Session = session;

        Audit = new AuditService(context, session);
        References = new ReferenceNumberService(context);
        Items = new ItemService(context, session, Audit);
        Recipients = new RecipientService(context, session, Audit);
        StockIn = new StockInService(context, session, Audit, References);
        Releases = new ReleaseService(context, session, Audit, References);
        Accountability = new AccountabilityService(context, session);
        History = new HistoryService(context, session);
        Dashboard = new DashboardService(context, session, Items, History);
    }

    public InventoryDbContext Context { get; }
    public CurrentUser Session { get; }
    public AuditService Audit { get; }
    public ReferenceNumberService References { get; }
    public ItemService Items { get; }
    public RecipientService Recipients { get; }
    public StockInService StockIn { get; }
    public ReleaseService Releases { get; }
    public AccountabilityService Accountability { get; }
    public HistoryService History { get; }
    public DashboardService Dashboard { get; }

    public static async Task<InventoryFixture> CreateAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"afms-test-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var context = new InventoryDbContext(options);
        await context.Database.MigrateAsync();

        var user = new User
        {
            Username = "tester",
            DisplayName = "Test Secretary",
            PasswordHash = "not-used-in-tests"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var session = new CurrentUser();
        session.Begin(new SignedInUser(user.Id, user.Username, user.DisplayName, false));

        return new InventoryFixture(databasePath, context, session);
    }

    // -----------------------------------------------------------------------
    // Convenience builders so each test reads as a story, not as setup
    // -----------------------------------------------------------------------

    public Task<ItemDto> AddItemAsync(
        string code,
        string name,
        bool tracksSerials = false,
        long reorderLevel = 0,
        string unit = "pcs") =>
        Items.SaveAsync(new SaveItemRequest(
            null, code, name, unit, null, tracksSerials, reorderLevel, null, true));

    public Task<RecipientDto> AddPersonAsync(string code, string name) =>
        Recipients.SaveAsync(new SaveRecipientRequest(null, code, name, null, null, null, null, true));

    public Task<StockInDto> ReceiveAsync(
        Guid itemId,
        long quantity,
        long? serialFrom = null,
        long? serialTo = null,
        StockInSource source = StockInSource.Purchase,
        Guid? returnedBy = null,
        DateOnly? date = null) =>
        StockIn.SaveAsync(new SaveStockInRequest(
            null,
            date ?? DateOnly.FromDateTime(DateTime.Today),
            source,
            source == StockInSource.Returned ? null : "Test supplier",
            "DR-001",
            returnedBy,
            "Test Secretary",
            null,
            [new StockLineInput(itemId, quantity, serialFrom, serialTo, null)]));

    public Task<ReleaseDto> ReleaseAsync(
        Guid recipientId,
        Guid itemId,
        long quantity,
        long? serialFrom = null,
        long? serialTo = null,
        DateOnly? date = null) =>
        Releases.SaveAsync(new SaveReleaseRequest(
            null,
            date ?? DateOnly.FromDateTime(DateTime.Today),
            recipientId,
            "Official use",
            "Test Secretary",
            "Recipient",
            null,
            [new StockLineInput(itemId, quantity, serialFrom, serialTo, null)]));

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try
            {
                var path = _databasePath + suffix;
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
                // A leftover temp file is harmless.
            }
        }
    }
}
