using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Infrastructure.Persistence;

internal sealed class FiscalYearRepository(AfmsDbContext context) : IFiscalYearRepository
{
    public async Task<IReadOnlyList<FiscalYear>> ListAsync(CancellationToken cancellationToken = default) =>
        await context.FiscalYears.AsNoTracking().OrderByDescending(x => x.Year).ToListAsync(cancellationToken);

    public Task<FiscalYear?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.FiscalYears.Include(x => x.Snapshots).ThenInclude(x => x.AccountableFormType)
            .Include(x => x.CarryForwards).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<FiscalYear?> FindForDateAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        context.FiscalYears.AsNoTracking().SingleOrDefaultAsync(x => x.StartDate <= date && date <= x.EndDate, cancellationToken);

    public Task<bool> YearExistsAsync(int year, CancellationToken cancellationToken = default) =>
        context.FiscalYears.AnyAsync(x => x.Year == year, cancellationToken);

    public Task AddAsync(FiscalYear year, CancellationToken cancellationToken = default) =>
        context.FiscalYears.AddAsync(year, cancellationToken).AsTask();

    public async Task<FiscalYearCloseCheck> CheckCloseAsync(FiscalYear year, CancellationToken cancellationToken = default)
    {
        var draftReceipts = await context.StockReceipts.CountAsync(x => x.ReceivedDate >= year.StartDate && x.ReceivedDate <= year.EndDate && x.Status == TransactionStatus.Draft, cancellationToken);
        var draftIssuances = await context.Issuances.CountAsync(x => x.IssuedDate >= year.StartDate && x.IssuedDate <= year.EndDate && x.Status == TransactionStatus.Draft, cancellationToken);
        var draftUsage = await context.UsageReports.CountAsync(x => x.ReportingDate >= year.StartDate && x.ReportingDate <= year.EndDate && x.Status == TransactionStatus.Draft, cancellationToken);
        var draftTransfers = await context.Transfers.CountAsync(x => x.TransferDate >= year.StartDate && x.TransferDate <= year.EndDate && x.Status == TransactionStatus.Draft, cancellationToken);
        var draftReturns = await context.ReturnTransactions.CountAsync(x => x.ReturnDate >= year.StartDate && x.ReturnDate <= year.EndDate && x.Status == TransactionStatus.Draft, cancellationToken);
        var draftExceptions = await context.FormExceptions.CountAsync(x => x.DateDiscovered >= year.StartDate && x.DateDiscovered <= year.EndDate && x.Status == TransactionStatus.Draft, cancellationToken);
        var drafts = draftReceipts + draftIssuances + draftUsage + draftTransfers + draftReturns + draftExceptions;

        var unresolved = await context.FormExceptions.CountAsync(x => x.DateDiscovered >= year.StartDate && x.DateDiscovered <= year.EndDate &&
            x.Status == TransactionStatus.Finalized && (x.ResolutionStatus == ExceptionResolutionStatus.Open || x.ResolutionStatus == ExceptionResolutionStatus.UnderInvestigation), cancellationToken);
        var awaitingFinalization = new[] { TransactionStatus.Pending, TransactionStatus.Completed, TransactionStatus.Submitted, TransactionStatus.Approved };
        var unapproved = await context.StockReceipts.CountAsync(x => x.ReceivedDate >= year.StartDate && x.ReceivedDate <= year.EndDate && awaitingFinalization.Contains(x.Status), cancellationToken)
            + await context.Issuances.CountAsync(x => x.IssuedDate >= year.StartDate && x.IssuedDate <= year.EndDate && awaitingFinalization.Contains(x.Status), cancellationToken)
            + await context.UsageReports.CountAsync(x => x.ReportingDate >= year.StartDate && x.ReportingDate <= year.EndDate && awaitingFinalization.Contains(x.Status), cancellationToken)
            + await context.Transfers.CountAsync(x => x.TransferDate >= year.StartDate && x.TransferDate <= year.EndDate && awaitingFinalization.Contains(x.Status), cancellationToken)
            + await context.ReturnTransactions.CountAsync(x => x.ReturnDate >= year.StartDate && x.ReturnDate <= year.EndDate && awaitingFinalization.Contains(x.Status), cancellationToken)
            + await context.FormExceptions.CountAsync(x => x.DateDiscovered >= year.StartDate && x.DateDiscovered <= year.EndDate && awaitingFinalization.Contains(x.Status), cancellationToken);

        var booklets = await context.AccountableFormBooklets.AsNoTracking().Where(x => x.DateReceived <= year.EndDate)
            .Include(x => x.AccountabilityRanges).ToListAsync(cancellationToken);
        var discrepancies = booklets.Count(x =>
        {
            var active = x.AccountabilityRanges.Where(r => r.IsActive).ToList();
            var rangeTotal = active.Count == 0 ? x.TotalQuantity : active.Sum(r => r.EndingSerial - r.BeginningSerial + 1);
            return rangeTotal != x.TotalQuantity || x.UsedQuantity < 0 || x.RemainingQuantity < 0 || x.UsedQuantity + x.RemainingQuantity != x.TotalQuantity;
        });

        var movements = await context.StockMovements.AsNoTracking().Where(x => x.EffectiveDate <= year.EndDate)
            .OrderBy(x => x.EffectiveDate).ThenBy(x => x.OccurredAt).ToListAsync(cancellationToken);
        var balances = new Dictionary<Guid, long>();
        var invalidCollectorIds = new HashSet<Guid>();
        foreach (var movement in movements)
        {
            if (movement.FromCollectorId is { } from)
            {
                balances[from] = checked(balances.GetValueOrDefault(from) - movement.Quantity);
                if (balances[from] < 0) invalidCollectorIds.Add(from);
            }
            if (movement.ToCollectorId is { } to)
            {
                balances[to] = checked(balances.GetValueOrDefault(to) + movement.Quantity);
                if (balances[to] < 0) invalidCollectorIds.Add(to);
            }
        }
        var invalidBalances = invalidCollectorIds.Count;

        var issues = new List<string>();
        if (drafts > 0) issues.Add($"{drafts} draft transaction(s) remain.");
        if (unresolved > 0) issues.Add($"{unresolved} unresolved exception(s) remain.");
        if (discrepancies > 0) issues.Add($"{discrepancies} inventory discrepancy record(s) were detected.");
        if (invalidBalances > 0) issues.Add($"{invalidBalances} invalid collector balance(s) were detected.");
        if (unapproved > 0) issues.Add($"{unapproved} submitted or approved transaction(s) are not finalized.");
        return new(drafts, unresolved, discrepancies, invalidBalances, unapproved, issues);
    }

    public async Task<IReadOnlyList<FiscalYearSnapshot>> BuildSnapshotAsync(FiscalYear year, CancellationToken cancellationToken = default)
    {
        var movements = await context.StockMovements.AsNoTracking().Include(x => x.AccountableFormType)
            .Where(x => x.EffectiveDate <= year.EndDate).ToListAsync(cancellationToken);
        var outstanding = BuildOwnershipAsOf(movements, year.EndDate);
        return movements.GroupBy(x => new { x.AccountableFormTypeId, x.AccountableFormType.Code, x.AccountableFormType.Name })
            .Select(group =>
            {
                var before = group.Where(x => Day(x) < year.StartDate).Sum(Signed);
                var period = group.Where(x => year.StartDate <= Day(x) && Day(x) <= year.EndDate).ToList();
                return new FiscalYearSnapshot
                {
                    FiscalYearId = year.Id,
                    AccountableFormTypeId = group.Key.AccountableFormTypeId,
                    BeginningQuantity = before,
                    Additions = period.Where(x => x.MovementType == StockMovementType.Received).Sum(x => x.Quantity),
                    Usage = period.Where(x => x.MovementType == StockMovementType.Used).Sum(x => x.Quantity),
                    Exceptions = period.Where(x => x.InventoryQuantityDelta < 0 && x.MovementType is not StockMovementType.Used and not StockMovementType.Reversed)
                        .Sum(x => -x.InventoryQuantityDelta),
                    EndingQuantity = before + period.Sum(Signed),
                    OutstandingQuantity = outstanding.Where(x => x.FormTypeId == group.Key.AccountableFormTypeId).Sum(x => x.Ending - x.Beginning + 1)
                };
            }).ToList();
    }

    public async Task<IReadOnlyList<FiscalYearCarryForward>> BuildCarryForwardAsync(FiscalYear year, CancellationToken cancellationToken = default)
    {
        var movements = await context.StockMovements.AsNoTracking().Where(x => x.EffectiveDate <= year.EndDate)
            .OrderBy(x => x.EffectiveDate).ThenBy(x => x.OccurredAt).ToListAsync(cancellationToken);
        var rows = BuildOwnershipAsOf(movements, year.EndDate);
        return rows.Select(x => new FiscalYearCarryForward
        {
            FiscalYearId = year.Id, TargetYear = year.Year + 1, AccountableFormBookletId = x.BookletId,
            BeginningSerial = x.Beginning, EndingSerial = x.Ending, Quantity = x.Ending - x.Beginning + 1,
            CollectorId = x.CollectorId, Status = x.Status
        }).ToList();
    }

    public Task AddSnapshotsAsync(IEnumerable<FiscalYearSnapshot> rows, CancellationToken cancellationToken = default) =>
        context.FiscalYearSnapshots.AddRangeAsync(rows, cancellationToken);

    public Task AddCarryForwardsAsync(IEnumerable<FiscalYearCarryForward> rows, CancellationToken cancellationToken = default) =>
        context.FiscalYearCarryForwards.AddRangeAsync(rows, cancellationToken);

    private static DateOnly Day(StockMovement x) => x.EffectiveDate;
    private static long Signed(StockMovement x) => x.InventoryQuantityDelta;

    private static IReadOnlyList<OwnershipRange> BuildOwnershipAsOf(IEnumerable<StockMovement> source, DateOnly cutoff)
    {
        var ownership = new List<OwnershipRange>();
        foreach (var movement in source.Where(movement => movement.EffectiveDate <= cutoff)
                     .OrderBy(movement => movement.EffectiveDate).ThenBy(movement => movement.OccurredAt).ThenBy(movement => movement.CreatedAt))
        {
            if (movement.FromCollectorId is { } from)
                RemoveOwnership(ownership, movement.AccountableFormBookletId, from, movement.BeginningSerial, movement.EndingSerial);
            if (movement.ToCollectorId is { } to)
            {
                var status = movement.MovementType == StockMovementType.Transferred ? AccountableFormStatus.Transferred : AccountableFormStatus.Issued;
                AddOwnership(ownership, new(movement.AccountableFormBookletId, movement.AccountableFormTypeId,
                    movement.BeginningSerial, movement.EndingSerial, to, status));
            }
        }
        return MergeOwnership(ownership);
    }

    private static void RemoveOwnership(List<OwnershipRange> ownership, Guid bookletId, Guid collectorId, long beginning, long ending)
    {
        var affected = ownership.Where(range => range.BookletId == bookletId && range.Beginning <= ending && beginning <= range.Ending).ToList();
        var covered = affected.Where(range => range.CollectorId == collectorId)
            .Sum(range => Math.Max(0, Math.Min(range.Ending, ending) - Math.Max(range.Beginning, beginning) + 1));
        var expected = checked(ending - beginning + 1);
        if (covered != expected || affected.Any(range => range.CollectorId != collectorId))
            throw new InvalidOperationException("Movement history contains an invalid or unowned collector range.");
        foreach (var range in affected)
        {
            ownership.Remove(range);
            if (range.Beginning < beginning) ownership.Add(range with { Ending = beginning - 1 });
            if (ending < range.Ending) ownership.Add(range with { Beginning = ending + 1 });
        }
    }

    private static void AddOwnership(List<OwnershipRange> ownership, OwnershipRange added)
    {
        if (ownership.Any(range => range.BookletId == added.BookletId && range.Beginning <= added.Ending && added.Beginning <= range.Ending))
            throw new InvalidOperationException("Movement history assigns the same serial range to more than one collector.");
        ownership.Add(added);
    }

    private static IReadOnlyList<OwnershipRange> MergeOwnership(IEnumerable<OwnershipRange> source)
    {
        var result = new List<OwnershipRange>();
        foreach (var current in source.OrderBy(range => range.BookletId).ThenBy(range => range.Beginning))
        {
            var previous = result.LastOrDefault();
            if (previous is not null && previous.BookletId == current.BookletId && previous.CollectorId == current.CollectorId &&
                previous.Status == current.Status && previous.Ending != long.MaxValue && previous.Ending + 1 == current.Beginning)
                result[^1] = previous with { Ending = current.Ending };
            else result.Add(current);
        }
        return result;
    }

    private sealed record OwnershipRange(Guid BookletId, Guid FormTypeId, long Beginning, long Ending, Guid CollectorId, AccountableFormStatus Status);
}
