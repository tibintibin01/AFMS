using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Infrastructure.Persistence;

public sealed class DashboardQueryService(AfmsDbContext context, IAuthorizationService authorization) : IDashboardQueryService
{
    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ViewDashboard);
        var ranges = context.AccountabilityRanges.AsNoTracking().Where(x => x.IsActive);
        var available = await ranges.Where(x => x.Status == AccountableFormStatus.Available).SumAsync(x => (long?)(x.EndingSerial - x.BeginningSerial + 1), cancellationToken) ?? 0;
        var outstanding = await ranges.Where(x => x.CurrentCollectorId != null && x.Status != AccountableFormStatus.FullyUsed).SumAsync(x => (long?)(x.EndingSerial - x.BeginningSerial + 1), cancellationToken) ?? 0;
        var activeCollectors = await context.Collectors.AsNoTracking().CountAsync(x => x.IsActive, cancellationToken);
        var drafts = await context.StockReceipts.CountAsync(x => x.Status == TransactionStatus.Draft, cancellationToken)
            + await context.Issuances.CountAsync(x => x.Status == TransactionStatus.Draft, cancellationToken)
            + await context.UsageReports.CountAsync(x => x.Status == TransactionStatus.Draft, cancellationToken)
            + await context.Transfers.CountAsync(x => x.Status == TransactionStatus.Draft, cancellationToken)
            + await context.ReturnTransactions.CountAsync(x => x.Status == TransactionStatus.Draft, cancellationToken)
            + await context.FormExceptions.CountAsync(x => x.Status == TransactionStatus.Draft, cancellationToken);
        var pending = await context.Issuances.CountAsync(x => x.Status == TransactionStatus.Submitted, cancellationToken)
            + await context.Transfers.CountAsync(x => x.Status == TransactionStatus.Submitted, cancellationToken)
            + await context.ReturnTransactions.CountAsync(x => x.Status == TransactionStatus.Submitted, cancellationToken)
            + await context.FormExceptions.CountAsync(x => x.Status == TransactionStatus.Submitted, cancellationToken);
        var unresolved = await context.FormExceptions.CountAsync(x => x.Status == TransactionStatus.Finalized && x.ResolutionStatus != ExceptionResolutionStatus.Resolved && x.ResolutionStatus != ExceptionResolutionStatus.Closed, cancellationToken);
        var movement = await context.StockMovements.MaxAsync(x => (DateTimeOffset?)x.OccurredAt, cancellationToken);
        return new(available, outstanding, activeCollectors, drafts, pending, unresolved, movement);
    }
}
