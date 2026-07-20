namespace AFMS.Application.Models;

public sealed record DashboardDto(
    long AvailableReceipts,
    long OutstandingReceipts,
    int ActiveCollectors,
    int DraftTransactions,
    int PendingApprovals,
    int UnresolvedExceptions,
    DateTimeOffset? LastStockMovement);
