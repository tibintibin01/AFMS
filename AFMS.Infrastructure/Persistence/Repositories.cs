using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Infrastructure.Persistence;

internal sealed class UserRepository(AfmsDbContext context) : IUserRepository
{
    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(cancellationToken);

    public Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default) =>
        context.Users
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .ThenInclude(role => role.Permissions)
            .SingleOrDefaultAsync(
                user => user.Username.ToUpper() == normalizedUsername,
                cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Users.Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role).ThenInclude(role => role.Permissions)
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<IReadOnlyList<User>> ListAsync(string? search, bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = context.Users.Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpper();
            query = query.Where(user => user.Username.ToUpper().Contains(term) || user.DisplayName.ToUpper().Contains(term));
        }
        if (isActive is not null) query = query.Where(user => user.IsActive == isActive);
        return await query.OrderBy(user => user.DisplayName).ToListAsync(cancellationToken);
    }

    public Task<bool> UsernameExistsAsync(string normalizedUsername, Guid? excludingId, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(user => user.Username.ToUpper() == normalizedUsername && (!excludingId.HasValue || user.Id != excludingId), cancellationToken);

    public Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default) =>
        context.Users.CountAsync(user => user.IsActive && user.UserRoles.Any(userRole =>
            userRole.IsActive && userRole.Role.IsActive && userRole.Role.Name == "Administrator"), cancellationToken);

    public Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        context.Users.AddAsync(user, cancellationToken).AsTask();
}

internal sealed class RoleRepository(AfmsDbContext context) : IRoleRepository
{
    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        context.Roles.SingleOrDefaultAsync(role => role.Name == name, cancellationToken);

    public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Roles.SingleOrDefaultAsync(role => role.Id == id && role.IsActive, cancellationToken);

    public async Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken = default) =>
        await context.Roles.Where(role => role.IsActive).OrderBy(role => role.Name).ToListAsync(cancellationToken);
}

internal sealed class AuditLogRepository(AfmsDbContext context) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        var pending = context.ChangeTracker.Entries<AuditLog>().Where(x => x.State == EntityState.Added)
            .Select(x => x.Entity).OrderByDescending(x => x.OccurredAt).FirstOrDefault();
        var previous = pending ?? await context.AuditLogs.AsNoTracking().OrderByDescending(x => x.OccurredAt).FirstOrDefaultAsync(cancellationToken);
        // Track the entry before hashing so EF relationship fix-up (for example,
        // AuditLog.User -> UserId during first-run setup) cannot change protected
        // values after the digest has been calculated.
        await context.AuditLogs.AddAsync(auditLog, cancellationToken);
        auditLog.PreviousHash = previous?.IntegrityHash;
        auditLog.IntegrityHash = AuditIntegrity.Hash(auditLog);
    }

    public async Task<IReadOnlyList<AuditLog>> ListAsync(Guid? userId, int limit, CancellationToken cancellationToken = default)
    {
        var query = context.AuditLogs.Include(audit => audit.User).AsQueryable();
        if (userId is not null) query = query.Where(audit => audit.UserId == userId);
        return await query.OrderByDescending(audit => audit.OccurredAt).Take(Math.Clamp(limit, 1, 1000)).ToListAsync(cancellationToken);
    }

    public async Task<AuditLogPage> QueryAsync(AuditLogFilter filter, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page); var size = Math.Clamp(filter.PageSize, 25, 500);
        var query = context.AuditLogs.AsNoTracking().Include(x => x.User).AsQueryable();
        if (filter.From is not null) query = query.Where(x => x.OccurredAt >= filter.From);
        if (filter.To is not null) query = query.Where(x => x.OccurredAt <= filter.To);
        if (filter.UserId is not null) query = query.Where(x => x.UserId == filter.UserId);
        if (filter.ActivityType is not null) query = query.Where(x => x.ActivityType == filter.ActivityType);
        if (!string.IsNullOrWhiteSpace(filter.Module)) { var module = filter.Module.Trim(); query = query.Where(x => x.Module == module); }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x => x.Description.Contains(term) || (x.ReferenceNumber != null && x.ReferenceNumber.Contains(term)) ||
                (x.EntityName != null && x.EntityName.Contains(term)) || (x.Reason != null && x.Reason.Contains(term)));
        }
        var total = await query.CountAsync(cancellationToken);
        var entries = await query.OrderByDescending(x => x.OccurredAt).Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new AuditLogPage(entries.Select(x => new AuditLogDto(x.OccurredAt, x.User?.Username ?? "System", x.ActivityType,
            x.Description, x.EntityName, x.EntityId, x.OldValues, x.NewValues, x.MachineName, x.Module,
            x.ReferenceNumber, x.Reason, x.IntegrityHash)).ToList(), total, page, size);
    }

}

internal sealed class AccountableFormTypeRepository(AfmsDbContext context) : IAccountableFormTypeRepository
{
    public async Task<IReadOnlyList<AccountableFormType>> ListAsync(string? search, bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = context.AccountableFormTypes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpper();
            query = query.Where(item => item.Code.ToUpper().Contains(term) || item.Name.ToUpper().Contains(term) || (item.Category != null && item.Category.ToUpper().Contains(term)));
        }
        if (isActive is not null) query = query.Where(item => item.IsActive == isActive);
        return await query.OrderBy(item => item.Code).ToListAsync(cancellationToken);
    }
    public Task<AccountableFormType?> GetAsync(Guid id, CancellationToken cancellationToken = default) => context.AccountableFormTypes.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    public Task<bool> CodeExistsAsync(string normalizedCode, Guid? excludingId, CancellationToken cancellationToken = default) =>
        context.AccountableFormTypes.AnyAsync(item => item.Code.ToUpper() == normalizedCode && (!excludingId.HasValue || item.Id != excludingId), cancellationToken);
    public Task<bool> HasTransactionHistoryAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.StockReceiptItems.AnyAsync(item => item.AccountableFormTypeId == id, cancellationToken);
    public Task AddAsync(AccountableFormType entity, CancellationToken cancellationToken = default) => context.AccountableFormTypes.AddAsync(entity, cancellationToken).AsTask();
}

internal sealed class BarangayRepository(AfmsDbContext context) : IBarangayRepository
{
    public async Task<IReadOnlyList<Barangay>> ListAsync(string? search, bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = context.Barangays.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpper();
            query = query.Where(item => item.Code.ToUpper().Contains(term) || item.Name.ToUpper().Contains(term) || item.Municipality.ToUpper().Contains(term));
        }
        if (isActive is not null) query = query.Where(item => item.IsActive == isActive);
        return await query.OrderBy(item => item.Name).ToListAsync(cancellationToken);
    }
    public Task<Barangay?> GetAsync(Guid id, CancellationToken cancellationToken = default) => context.Barangays.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    public Task<bool> CodeExistsAsync(string normalizedCode, Guid? excludingId, CancellationToken cancellationToken = default) =>
        context.Barangays.AnyAsync(item => item.Code.ToUpper() == normalizedCode && (!excludingId.HasValue || item.Id != excludingId), cancellationToken);
    public Task<bool> NameExistsAsync(string normalizedName, Guid? excludingId, CancellationToken cancellationToken = default) =>
        context.Barangays.AnyAsync(item => item.Name.ToUpper() == normalizedName && (!excludingId.HasValue || item.Id != excludingId), cancellationToken);
    public Task AddAsync(Barangay entity, CancellationToken cancellationToken = default) => context.Barangays.AddAsync(entity, cancellationToken).AsTask();
}

internal sealed class CollectorRepository(AfmsDbContext context) : ICollectorRepository
{
    public async Task<IReadOnlyList<Collector>> ListAsync(string? search, Guid? barangayId, bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = context.Collectors.Include(item => item.Barangay).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpper();
            query = query.Where(item => item.Code.ToUpper().Contains(term) || item.FullName.ToUpper().Contains(term) || (item.EmployeeNumber != null && item.EmployeeNumber.ToUpper().Contains(term)));
        }
        if (barangayId is not null) query = query.Where(item => item.BarangayId == barangayId);
        if (isActive is not null) query = query.Where(item => item.IsActive == isActive);
        return await query.OrderBy(item => item.FullName).ToListAsync(cancellationToken);
    }
    public Task<Collector?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Collectors.Include(item => item.Barangay).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    public Task<bool> NumberExistsAsync(string normalizedNumber, Guid? excludingId, CancellationToken cancellationToken = default) =>
        context.Collectors.AnyAsync(item => item.Code.ToUpper() == normalizedNumber && (!excludingId.HasValue || item.Id != excludingId), cancellationToken);
    public async Task<bool> HasTransactionHistoryAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Issuances.AnyAsync(item => item.CollectorId == id, cancellationToken) ||
        await context.UsageReports.AnyAsync(item => item.CollectorId == id, cancellationToken) ||
        await context.Transfers.AnyAsync(item => item.FromCollectorId == id || item.ToCollectorId == id, cancellationToken) ||
        await context.ReturnTransactions.AnyAsync(item => item.CollectorId == id, cancellationToken) ||
        await context.FormExceptions.AnyAsync(item => item.CollectorId == id, cancellationToken);
    public Task AddAsync(Collector entity, CancellationToken cancellationToken = default) => context.Collectors.AddAsync(entity, cancellationToken).AsTask();
}

internal sealed class SystemSettingRepository(AfmsDbContext context) : ISystemSettingRepository
{
    public async Task<IReadOnlyList<SystemSetting>> ListAsync(CancellationToken cancellationToken = default) =>
        await context.SystemSettings.OrderBy(item => item.Key).ToListAsync(cancellationToken);
    public Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        context.SystemSettings.SingleOrDefaultAsync(item => item.Key == key, cancellationToken);
    public Task AddAsync(SystemSetting setting, CancellationToken cancellationToken = default) =>
        context.SystemSettings.AddAsync(setting, cancellationToken).AsTask();
}

internal sealed class ReferenceNumberSequenceRepository(AfmsDbContext context, IDatabaseOperationCoordinator coordinator) : IReferenceNumberSequenceRepository
{
    public async Task<long> ReserveNextAsync(string prefix, int year, CancellationToken cancellationToken = default)
    {
        await using var operationLease = await coordinator.EnterWriteAsync(cancellationToken);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
            try
            {
                var sequence = await context.ReferenceNumberSequences
                    .SingleOrDefaultAsync(item => item.Prefix == prefix && item.Year == year, cancellationToken);
                if (sequence is null)
                {
                    sequence = new ReferenceNumberSequence { Prefix = prefix, Year = year, LastNumber = 1 };
                    context.ReferenceNumberSequences.Add(sequence);
                }
                else
                {
                    sequence.LastNumber = checked(sequence.LastNumber + 1);
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return sequence.LastNumber;
            }
            catch (Exception exception) when (attempt < 5 && exception is DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
            {
                await transaction.RollbackAsync(cancellationToken);
                context.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException("A unique reference number could not be reserved after multiple attempts.");
    }
}

internal sealed class TransactionRunner(AfmsDbContext context, IDatabaseOperationCoordinator coordinator) : ITransactionRunner
{
    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        await using var operationLease = await coordinator.EnterWriteAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            throw;
        }
    }
}

internal sealed class StockReceiptRepository(AfmsDbContext context) : IStockReceiptRepository
{
    private IQueryable<StockReceipt> CompleteQuery() => context.StockReceipts
        .Include(item => item.AccountableFormType)
        .Include(item => item.EncodedByUser)
        .Include(item => item.Items)
            .ThenInclude(item => item.Booklets)
                .ThenInclude(item => item.Movements);

    public async Task<IReadOnlyList<StockReceipt>> ListAsync(CancellationToken cancellationToken = default) =>
        await CompleteQuery().OrderByDescending(item => item.ReceivedDate).ThenByDescending(item => item.ReferenceNumber).ToListAsync(cancellationToken);

    public Task<StockReceipt?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        CompleteQuery().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task AddAsync(StockReceipt receipt, CancellationToken cancellationToken = default) =>
        context.StockReceipts.AddAsync(receipt, cancellationToken).AsTask();

    public void RemoveItems(IEnumerable<StockReceiptItem> items) => context.StockReceiptItems.RemoveRange(items);

    public Task<bool> HasOverlappingReceiptRangeAsync(Guid formTypeId, int seriesYear, long beginning, long ending, Guid? excludingReceiptId, CancellationToken cancellationToken = default) =>
        context.StockReceiptItems.AnyAsync(item =>
            item.AccountableFormTypeId == formTypeId && item.StockReceipt.SeriesYear == seriesYear &&
            item.StockReceipt.Status != AFMS.Domain.Enums.TransactionStatus.Reversed &&
            (!excludingReceiptId.HasValue || item.StockReceiptId != excludingReceiptId) &&
            beginning <= item.EndingSerial && item.BeginningSerial <= ending, cancellationToken);

    public Task<bool> HasOverlappingBookletRangeAsync(Guid formTypeId, int seriesYear, long beginning, long ending, CancellationToken cancellationToken = default) =>
        context.AccountableFormBooklets.AnyAsync(item => item.AccountableFormTypeId == formTypeId && item.SeriesYear == seriesYear &&
            item.Status != AFMS.Domain.Enums.AccountableFormStatus.Archived && beginning <= item.EndingSerial && item.BeginningSerial <= ending, cancellationToken);

    public Task AddBookletsAsync(IEnumerable<AccountableFormBooklet> booklets, CancellationToken cancellationToken = default) =>
        context.AccountableFormBooklets.AddRangeAsync(booklets, cancellationToken);

    public Task AddMovementsAsync(IEnumerable<StockMovement> movements, CancellationToken cancellationToken = default) =>
        context.StockMovements.AddRangeAsync(movements, cancellationToken);

    public Task AddAccountabilityRangeAsync(AccountabilityRange range, CancellationToken cancellationToken = default) =>
        context.AccountabilityRanges.AddAsync(range, cancellationToken).AsTask();
}

internal sealed class InventoryRepository(AfmsDbContext context) : IInventoryRepository
{
    private IQueryable<AccountableFormBooklet> BaseQuery() => context.AccountableFormBooklets.AsNoTracking()
        .Include(item => item.AccountableFormType)
        .Include(item => item.CurrentCollector)
        .Include(item => item.CurrentBarangay)
        .Include(item => item.AccountabilityRanges).ThenInclude(range => range.CurrentCollector)
        .Include(item => item.AccountabilityRanges).ThenInclude(range => range.CurrentBarangay)
        .Include(item => item.Movements)
            .ThenInclude(item => item.PerformedByUser)
        .Include(item => item.Movements)
            .ThenInclude(item => item.AccountableFormType)
        .Include(item => item.StockReceiptItem)
            .ThenInclude(item => item.StockReceipt);

    public async Task<IReadOnlyList<AccountableFormBooklet>> ListAsync(InventoryFilter filter, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery();
        if (filter.AccountableFormTypeId is not null) query = query.Where(item => item.AccountableFormTypeId == filter.AccountableFormTypeId);
        if (filter.SeriesYear is not null) query = query.Where(item => item.SeriesYear == filter.SeriesYear);
        if (filter.Status is not null) query = query.Where(item => item.AccountabilityRanges.Any(range => range.IsActive && range.Status == filter.Status));
        if (filter.CollectorId is not null) query = query.Where(item => item.AccountabilityRanges.Any(range => range.IsActive && range.CurrentCollectorId == filter.CollectorId));
        if (filter.BarangayId is not null) query = query.Where(item => item.AccountabilityRanges.Any(range => range.IsActive && range.CurrentBarangayId == filter.BarangayId));
        if (filter.ReceivedFrom is not null) query = query.Where(item => item.DateReceived >= filter.ReceivedFrom);
        if (filter.ReceivedTo is not null) query = query.Where(item => item.DateReceived <= filter.ReceivedTo);
        if (filter.SerialNumber is not null) query = query.Where(item => item.BeginningSerial <= filter.SerialNumber && filter.SerialNumber <= item.EndingSerial);
        if (filter.AvailableOnly) query = query.Where(item => item.AccountabilityRanges.Any(range => range.IsActive && range.Status == AFMS.Domain.Enums.AccountableFormStatus.Available));
        if (filter.OutstandingOnly) query = query.Where(item => item.AccountabilityRanges.Any(range => range.IsActive && range.CurrentCollectorId != null &&
            range.Status != AFMS.Domain.Enums.AccountableFormStatus.Archived && range.Status != AFMS.Domain.Enums.AccountableFormStatus.FullyUsed));
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToUpper();
            query = query.Where(item => item.AccountableFormType.Code.ToUpper().Contains(term) ||
                                        item.AccountableFormType.Name.ToUpper().Contains(term) ||
                                        (item.StorageLocation != null && item.StorageLocation.ToUpper().Contains(term)) ||
                                        item.AccountabilityRanges.Any(range => range.IsActive && range.CurrentCollector != null && range.CurrentCollector.FullName.ToUpper().Contains(term)) ||
                                        item.StockReceiptItem.StockReceipt.ReferenceNumber.ToUpper().Contains(term));
        }
        return await query.OrderBy(item => item.AccountableFormType.Code).ThenBy(item => item.SeriesYear).ThenBy(item => item.BeginningSerial).ToListAsync(cancellationToken);
    }

    public Task<AccountableFormBooklet?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default) =>
        BaseQuery()
            .Include(item => item.UsageRanges)
            .Include(item => item.Exceptions)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<StockMovement>> ListMovementsAsync(MovementHistoryFilter filter, CancellationToken cancellationToken = default)
    {
        var query = context.StockMovements
            .AsNoTracking()
            .Include(item => item.AccountableFormType)
            .Include(item => item.PerformedByUser)
            .AsQueryable();
        if (filter.MovementType is not null) query = query.Where(item => item.MovementType == filter.MovementType);
        if (filter.AccountableFormTypeId is not null) query = query.Where(item => item.AccountableFormTypeId == filter.AccountableFormTypeId);
        if (filter.FromDate is not null) query = query.Where(item => item.EffectiveDate >= filter.FromDate.Value);
        if (filter.ToDate is not null) query = query.Where(item => item.EffectiveDate <= filter.ToDate.Value);
        if (filter.SerialNumber is not null) query = query.Where(item => item.BeginningSerial <= filter.SerialNumber && filter.SerialNumber <= item.EndingSerial);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToUpper();
            query = query.Where(item => item.ReferenceNumber.ToUpper().Contains(term) ||
                                        item.AccountableFormType.Code.ToUpper().Contains(term) ||
                                        item.AccountableFormType.Name.ToUpper().Contains(term) ||
                                        (item.Source != null && item.Source.ToUpper().Contains(term)) ||
                                        (item.Destination != null && item.Destination.ToUpper().Contains(term)));
        }
        var entries = await query.ToListAsync(cancellationToken);
        return entries.OrderByDescending(item => item.EffectiveDate).ThenByDescending(item => item.OccurredAt).ToList();
    }
}

internal sealed class IssuanceRepository(AfmsDbContext context) : IIssuanceRepository
{
    private IQueryable<Issuance> CompleteQuery() => context.Issuances
        .Include(item => item.Collector)
        .Include(item => item.Barangay)
        .Include(item => item.IssuedByUser)
        .Include(item => item.ApprovedByUser)
        .Include(item => item.Items)
            .ThenInclude(item => item.AccountableFormBooklet)
                .ThenInclude(item => item.AccountableFormType)
        .Include(item => item.Items)
            .ThenInclude(item => item.AccountableFormBooklet)
                .ThenInclude(item => item.AccountabilityRanges);

    public async Task<IReadOnlyList<Issuance>> ListAsync(CancellationToken cancellationToken = default) =>
        await CompleteQuery().OrderByDescending(item => item.IssuedDate).ThenByDescending(item => item.ReferenceNumber).ToListAsync(cancellationToken);

    public Task<Issuance?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        CompleteQuery().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task AddAsync(Issuance issuance, CancellationToken cancellationToken = default) =>
        context.Issuances.AddAsync(issuance, cancellationToken).AsTask();

    public void RemoveItems(IEnumerable<IssuanceItem> items) => context.IssuanceItems.RemoveRange(items);

    public async Task<IReadOnlyList<AccountabilityRange>> ListAvailableBookletsAsync(AvailableBookletFilter filter, Guid? excludingIssuanceId, CancellationToken cancellationToken = default)
    {
        var query = context.AccountabilityRanges.AsNoTracking()
            .Include(range => range.AccountableFormBooklet).ThenInclude(booklet => booklet.AccountableFormType)
            .Include(range => range.AccountableFormBooklet).ThenInclude(booklet => booklet.StockReceiptItem).ThenInclude(item => item.StockReceipt).ThenInclude(receipt => receipt.FiscalYear)
            .Where(range => range.IsActive && range.Status == AFMS.Domain.Enums.AccountableFormStatus.Available && range.CurrentCollectorId == null &&
                           (range.AccountableFormBooklet.StockReceiptItem.StockReceipt.FiscalYear == null || !range.AccountableFormBooklet.StockReceiptItem.StockReceipt.FiscalYear.IsClosed) &&
                           !range.AccountableFormBooklet.IssuanceItems.Any(reservation => reservation.IsReservationActive &&
                               (!excludingIssuanceId.HasValue || reservation.IssuanceId != excludingIssuanceId) &&
                               reservation.BeginningSerial <= range.EndingSerial && range.BeginningSerial <= reservation.EndingSerial));
        if (filter.FormTypeId is not null) query = query.Where(range => range.AccountableFormBooklet.AccountableFormTypeId == filter.FormTypeId);
        if (filter.SeriesYear is not null) query = query.Where(range => range.AccountableFormBooklet.SeriesYear == filter.SeriesYear);
        if (!string.IsNullOrWhiteSpace(filter.StorageLocation))
        {
            var term = filter.StorageLocation.Trim().ToUpper();
            query = query.Where(range => range.StorageLocation != null && range.StorageLocation.ToUpper().Contains(term));
        }
        if (filter.BeginningSerial is not null) query = query.Where(range => range.BeginningSerial >= filter.BeginningSerial);
        if (filter.EndingSerial is not null) query = query.Where(range => range.EndingSerial <= filter.EndingSerial);
        return await query.OrderBy(range => range.AccountableFormBooklet.AccountableFormType.Code)
            .ThenBy(range => range.AccountableFormBooklet.SeriesYear).ThenBy(range => range.BeginningSerial).ToListAsync(cancellationToken);
    }

    public Task<bool> IsRangeIssuableAsync(Guid bookletId, long beginningSerial, long endingSerial, Guid? excludingIssuanceId, CancellationToken cancellationToken = default) =>
        context.AccountabilityRanges.AnyAsync(range => range.AccountableFormBookletId == bookletId && range.IsActive &&
            range.Status == AFMS.Domain.Enums.AccountableFormStatus.Available && range.CurrentCollectorId == null &&
            range.BeginningSerial == beginningSerial && range.EndingSerial == endingSerial &&
            (range.AccountableFormBooklet.StockReceiptItem.StockReceipt.FiscalYear == null || !range.AccountableFormBooklet.StockReceiptItem.StockReceipt.FiscalYear.IsClosed) &&
            !range.AccountableFormBooklet.IssuanceItems.Any(reservation => reservation.IsReservationActive &&
                (!excludingIssuanceId.HasValue || reservation.IssuanceId != excludingIssuanceId) &&
                reservation.BeginningSerial <= endingSerial && beginningSerial <= reservation.EndingSerial), cancellationToken);

    public Task AddMovementsAsync(IEnumerable<StockMovement> movements, CancellationToken cancellationToken = default) =>
        context.StockMovements.AddRangeAsync(movements, cancellationToken);

    public Task AddAccountabilityRangeAsync(AccountabilityRange range, CancellationToken cancellationToken = default) =>
        context.AccountabilityRanges.AddAsync(range, cancellationToken).AsTask();
}

internal sealed class CollectorAccountabilityRepository(AfmsDbContext context) : ICollectorAccountabilityRepository
{
    public async Task<IReadOnlyList<StockMovement>> ListCollectorMovementsAsync(Guid collectorId, Guid? formTypeId, Guid? barangayId, bool outstandingOnly, CancellationToken cancellationToken = default)
    {
        var query = context.StockMovements
            .Include(item => item.AccountableFormType)
            .Include(item => item.AccountableFormBooklet).ThenInclude(item => item.AccountableFormType)
            .Include(item => item.AccountableFormBooklet).ThenInclude(item => item.CurrentBarangay)
            .Include(item => item.AccountableFormBooklet).ThenInclude(item => item.AccountabilityRanges)
            .Include(item => item.Barangay)
            .Where(item => item.FromCollectorId == collectorId || item.ToCollectorId == collectorId);
        if (formTypeId is not null) query = query.Where(item => item.AccountableFormTypeId == formTypeId);
        if (barangayId is not null) query = query.Where(item => item.BarangayId == barangayId);
        if (outstandingOnly) query = query.Where(item => item.AccountableFormBooklet.AccountabilityRanges.Any(range => range.IsActive && range.CurrentCollectorId == collectorId));
        var entries = await query.ToListAsync(cancellationToken);
        return entries.OrderBy(item => item.EffectiveDate).ThenBy(item => item.OccurredAt).ThenBy(item => item.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<AccountableFormBooklet>> ListCollectorBookletsAsync(Guid collectorId, CancellationToken cancellationToken = default) =>
        await context.AccountableFormBooklets
            .Include(item => item.AccountableFormType)
            .Include(item => item.CurrentCollector)
            .Include(item => item.CurrentBarangay)
            .Include(item => item.Movements)
            .Include(item => item.AccountabilityRanges)
            .Where(item => item.AccountabilityRanges.Any(range => range.IsActive && range.CurrentCollectorId == collectorId) || item.Movements.Any(movement => movement.FromCollectorId == collectorId || movement.ToCollectorId == collectorId))
            .OrderBy(item => item.AccountableFormType.Code).ThenBy(item => item.BeginningSerial)
            .ToListAsync(cancellationToken);
}

internal sealed class OperationalTransactionRepository(AfmsDbContext context) : IOperationalTransactionRepository
{
    private IQueryable<AccountableFormBooklet> Booklets() => context.AccountableFormBooklets
        .Include(x => x.AccountableFormType).Include(x => x.AccountabilityRanges).ThenInclude(x => x.CurrentCollector)
        .Include(x => x.AccountabilityRanges).ThenInclude(x => x.CurrentBarangay)
        .Include(x => x.IssuanceItems).ThenInclude(x => x.Issuance);

    public async Task<IReadOnlyList<AccountabilityRange>> ListOwnedRangesAsync(Guid? collectorId, Guid? formTypeId, CancellationToken ct = default)
    {
        var query = context.AccountabilityRanges.Include(x => x.AccountableFormBooklet).ThenInclude(x => x.AccountableFormType)
            .Include(x => x.CurrentCollector).Include(x => x.CurrentBarangay)
            .Where(x => x.IsActive && x.CurrentCollectorId != null &&
                (x.Status == AFMS.Domain.Enums.AccountableFormStatus.Issued || x.Status == AFMS.Domain.Enums.AccountableFormStatus.PartiallyUsed || x.Status == AFMS.Domain.Enums.AccountableFormStatus.Transferred));
        if (collectorId is not null) query = query.Where(x => x.CurrentCollectorId == collectorId);
        if (formTypeId is not null) query = query.Where(x => x.AccountableFormBooklet.AccountableFormTypeId == formTypeId);
        return await query.OrderBy(x => x.AccountableFormBooklet.AccountableFormType.Code).ThenBy(x => x.BeginningSerial).ToListAsync(ct);
    }

    public Task<AccountableFormBooklet?> GetBookletAsync(Guid id, CancellationToken ct = default) => Booklets().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<Issuance?> FindSourceIssuanceAsync(Guid bookletId, Guid collectorId, CancellationToken ct = default)
    {
        var matches = await context.Issuances.Where(x => x.CollectorId == collectorId && x.Status == AFMS.Domain.Enums.TransactionStatus.Finalized &&
            x.Items.Any(i => i.AccountableFormBookletId == bookletId)).ToListAsync(ct);
        return matches.OrderByDescending(x => x.FinalizedAt).FirstOrDefault();
    }

    private IQueryable<UsageReport> Usage() => context.UsageReports.Include(x => x.Collector).Include(x => x.AccountableFormType)
        .Include(x => x.Ranges).ThenInclude(x => x.AccountableFormBooklet).ThenInclude(x => x.AccountableFormType);
    public async Task<IReadOnlyList<UsageReport>> ListUsageAsync(CancellationToken ct = default) => await Usage().OrderByDescending(x => x.ReportingDate).ToListAsync(ct);
    public Task<UsageReport?> GetUsageAsync(Guid id, CancellationToken ct = default) => Usage().SingleOrDefaultAsync(x => x.Id == id, ct);

    private IQueryable<Transfer> TransferQuery() => context.Transfers.Include(x => x.FromCollector).Include(x => x.ToCollector).Include(x => x.Barangay)
        .Include(x => x.AccountableFormBooklet).ThenInclude(x => x.AccountableFormType);
    public async Task<IReadOnlyList<Transfer>> ListTransfersAsync(CancellationToken ct = default) => await TransferQuery().OrderByDescending(x => x.TransferDate).ToListAsync(ct);
    public Task<Transfer?> GetTransferAsync(Guid id, CancellationToken ct = default) => TransferQuery().SingleOrDefaultAsync(x => x.Id == id, ct);

    private IQueryable<ReturnTransaction> ReturnQuery() => context.ReturnTransactions.Include(x => x.Collector)
        .Include(x => x.AccountableFormBooklet).ThenInclude(x => x.AccountableFormType);
    public async Task<IReadOnlyList<ReturnTransaction>> ListReturnsAsync(CancellationToken ct = default) => await ReturnQuery().OrderByDescending(x => x.ReturnDate).ToListAsync(ct);
    public Task<ReturnTransaction?> GetReturnAsync(Guid id, CancellationToken ct = default) => ReturnQuery().SingleOrDefaultAsync(x => x.Id == id, ct);

    private IQueryable<FormException> ExceptionQuery() => context.FormExceptions.Include(x => x.Collector)
        .Include(x => x.AccountableFormBooklet).ThenInclude(x => x.AccountableFormType);
    public async Task<IReadOnlyList<FormException>> ListExceptionsAsync(CancellationToken ct = default) => await ExceptionQuery().OrderByDescending(x => x.DateDiscovered).ToListAsync(ct);
    public Task<FormException?> GetExceptionAsync(Guid id, CancellationToken ct = default) => ExceptionQuery().SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task AddAsync<T>(T entity, CancellationToken ct = default) where T : class => context.Set<T>().AddAsync(entity, ct).AsTask();
    public void RemoveRange(IEnumerable<UsageRange> ranges) => context.UsageRanges.RemoveRange(ranges);
    public Task AddMovementsAsync(IEnumerable<StockMovement> movements, CancellationToken ct = default) => context.StockMovements.AddRangeAsync(movements, ct);
    public async Task<IReadOnlyList<TransactionAttachment>> ListAttachmentsAsync(AFMS.Domain.Enums.AttachmentTransactionType type, Guid transactionId, CancellationToken ct = default) =>
        await context.TransactionAttachments.Where(x => x.RelatedTransactionType == type && x.RelatedTransactionId == transactionId).OrderByDescending(x => x.UploadDate).ToListAsync(ct);
}

internal sealed class ReportDataRepository(AfmsDbContext context) : IReportDataRepository
{
    public async Task<ReportDataSnapshot> LoadAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var from = filter.FromDate ?? (filter.FiscalYear is null ? null : new DateOnly(filter.FiscalYear.Value, 1, 1));
        var to = filter.ToDate ?? (filter.FiscalYear is null ? null : new DateOnly(filter.FiscalYear.Value, 12, 31));
        bool Allowed(AFMS.Domain.Enums.TransactionStatus status) => filter.TransactionStatus is not null
            ? status == filter.TransactionStatus
            : filter.IncludeDrafts || status is AFMS.Domain.Enums.TransactionStatus.Finalized or AFMS.Domain.Enums.TransactionStatus.Reversed;
        bool Ref(string value) => string.IsNullOrWhiteSpace(filter.ReferenceNumber) || value.Contains(filter.ReferenceNumber.Trim(), StringComparison.OrdinalIgnoreCase);
        bool Date(DateOnly date) => (from is null || date >= from) && (to is null || date <= to);

        var booklets = await context.AccountableFormBooklets.AsNoTrackingWithIdentityResolution()
            .Include(x => x.AccountableFormType).Include(x => x.CurrentCollector).Include(x => x.CurrentBarangay)
            .Include(x => x.AccountabilityRanges).ThenInclude(x => x.CurrentCollector)
            .Include(x => x.AccountabilityRanges).ThenInclude(x => x.CurrentBarangay)
            .ToListAsync(ct);
        var formBooklets = booklets.Where(x => filter.FormTypeId is null || x.AccountableFormTypeId == filter.FormTypeId).ToList();
        var ranges = formBooklets.SelectMany(x => x.AccountabilityRanges).Where(x => x.IsActive &&
            (filter.CollectorId is null || x.CurrentCollectorId == filter.CollectorId) &&
            (filter.BarangayId is null || x.CurrentBarangayId == filter.BarangayId) &&
            (filter.Status is null || x.Status == filter.Status) &&
            (!filter.OutstandingOnly || x.CurrentCollectorId != null)).ToList();
        ranges.AddRange(formBooklets.Where(x => !x.AccountabilityRanges.Any(range => range.IsActive))
            .Select(x => new AccountabilityRange
            {
                AccountableFormBookletId = x.Id,
                AccountableFormBooklet = x,
                BeginningSerial = x.BeginningSerial,
                EndingSerial = x.EndingSerial,
                Status = x.Status,
                CurrentCollectorId = x.CurrentCollectorId,
                CurrentCollector = x.CurrentCollector,
                CurrentBarangayId = x.CurrentBarangayId,
                CurrentBarangay = x.CurrentBarangay,
                StorageLocation = x.StorageLocation
            })
            .Where(x => (filter.CollectorId is null || x.CurrentCollectorId == filter.CollectorId) &&
                (filter.BarangayId is null || x.CurrentBarangayId == filter.BarangayId) &&
                (filter.Status is null || x.Status == filter.Status) &&
                (!filter.OutstandingOnly || x.CurrentCollectorId != null)));
        booklets = formBooklets.Where(x => (filter.Status is null || x.Status == filter.Status) &&
            (filter.CollectorId is null || x.CurrentCollectorId == filter.CollectorId) &&
            (filter.BarangayId is null || x.CurrentBarangayId == filter.BarangayId) &&
            (!filter.OutstandingOnly || x.CurrentCollectorId != null)).ToList();

        var movements = await context.StockMovements.AsNoTrackingWithIdentityResolution().Include(x => x.AccountableFormType)
            .Include(x => x.AccountableFormBooklet).ThenInclude(x => x.AccountableFormType)
            .Include(x => x.FromCollector).Include(x => x.ToCollector).Include(x => x.Barangay).Include(x => x.PerformedByUser).ToListAsync(ct);
        movements = movements.Where(x => (to is null || x.EffectiveDate <= to) &&
            (filter.FormTypeId is null || x.AccountableFormTypeId == filter.FormTypeId) &&
            (filter.CollectorId is null || x.FromCollectorId == filter.CollectorId || x.ToCollectorId == filter.CollectorId) &&
            (filter.BarangayId is null || x.BarangayId == filter.BarangayId) && Ref(x.ReferenceNumber)).ToList();

        var receipts = await context.StockReceipts.AsNoTrackingWithIdentityResolution().Include(x => x.AccountableFormType).Include(x => x.Items).ToListAsync(ct);
        receipts = receipts.Where(x => Allowed(x.Status) && Date(x.ReceivedDate) && Ref(x.ReferenceNumber) && (filter.FormTypeId is null || x.AccountableFormTypeId == filter.FormTypeId)).ToList();
        var issuances = await context.Issuances.AsNoTrackingWithIdentityResolution().Include(x => x.Collector).Include(x => x.Barangay)
            .Include(x => x.Items).ThenInclude(x => x.AccountableFormBooklet).ThenInclude(x => x.AccountableFormType).ToListAsync(ct);
        issuances = issuances.Where(x => Allowed(x.Status) && Date(x.IssuedDate) && Ref(x.ReferenceNumber) &&
            (filter.CollectorId is null || x.CollectorId == filter.CollectorId) && (filter.BarangayId is null || x.BarangayId == filter.BarangayId) &&
            (filter.FormTypeId is null || x.Items.Any(i => i.AccountableFormBooklet.AccountableFormTypeId == filter.FormTypeId))).ToList();
        if (filter.FormTypeId is not null)
            foreach (var issuance in issuances)
                issuance.Items = issuance.Items.Where(x => x.AccountableFormBooklet.AccountableFormTypeId == filter.FormTypeId).ToList();
        var usage = await context.UsageReports.AsNoTrackingWithIdentityResolution().Include(x => x.Collector).Include(x => x.AccountableFormType)
            .Include(x => x.Ranges).ThenInclude(x => x.AccountableFormBooklet).ThenInclude(x => x.AccountableFormType).ToListAsync(ct);
        usage = usage.Where(x => Allowed(x.Status) && Date(x.ReportingDate) && Ref(x.ReferenceNumber) &&
            (filter.CollectorId is null || x.CollectorId == filter.CollectorId) && (filter.FormTypeId is null || x.AccountableFormTypeId == filter.FormTypeId)).ToList();
        var transfers = await context.Transfers.AsNoTrackingWithIdentityResolution().Include(x => x.FromCollector).Include(x => x.ToCollector).Include(x => x.Barangay)
            .Include(x => x.AccountableFormBooklet).ThenInclude(x => x.AccountableFormType).ToListAsync(ct);
        transfers = transfers.Where(x => Allowed(x.Status) && Date(x.TransferDate) && Ref(x.ReferenceNumber) &&
            (filter.CollectorId is null || x.FromCollectorId == filter.CollectorId || x.ToCollectorId == filter.CollectorId) &&
            (filter.BarangayId is null || x.BarangayId == filter.BarangayId) && (filter.FormTypeId is null || x.AccountableFormBooklet.AccountableFormTypeId == filter.FormTypeId)).ToList();
        var returns = await context.ReturnTransactions.AsNoTrackingWithIdentityResolution().Include(x => x.Collector)
            .Include(x => x.AccountableFormBooklet).ThenInclude(x => x.AccountableFormType).ToListAsync(ct);
        returns = returns.Where(x => Allowed(x.Status) && Date(x.ReturnDate) && Ref(x.ReferenceNumber) &&
            (filter.CollectorId is null || x.CollectorId == filter.CollectorId) && (filter.FormTypeId is null || x.AccountableFormBooklet.AccountableFormTypeId == filter.FormTypeId)).ToList();
        var exceptions = await context.FormExceptions.AsNoTrackingWithIdentityResolution().Include(x => x.Collector)
            .Include(x => x.AccountableFormBooklet).ThenInclude(x => x.AccountableFormType).ToListAsync(ct);
        exceptions = exceptions.Where(x => Allowed(x.Status) && Date(x.DateDiscovered) && Ref(x.ReferenceNumber) &&
            (filter.CollectorId is null || x.CollectorId == filter.CollectorId) && (filter.FormTypeId is null || x.AccountableFormBooklet.AccountableFormTypeId == filter.FormTypeId)).ToList();
        var collectors = await context.Collectors.AsNoTracking().Include(x => x.Barangay).Where(x => filter.CollectorId == null || x.Id == filter.CollectorId).ToListAsync(ct);
        var audits = await context.AuditLogs.AsNoTracking().Include(x => x.User).ToListAsync(ct);
        audits = audits.Where(x => (from is null || DateOnly.FromDateTime(x.OccurredAt.LocalDateTime) >= from) &&
            (to is null || DateOnly.FromDateTime(x.OccurredAt.LocalDateTime) <= to) && Ref(x.Description)).ToList();
        return new(booklets, ranges, movements, receipts, issuances, usage, transfers, returns, exceptions, collectors, audits);
    }
}
