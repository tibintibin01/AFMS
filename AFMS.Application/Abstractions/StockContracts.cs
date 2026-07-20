using AFMS.Application.Models;
using AFMS.Domain.Entities;

namespace AFMS.Application.Abstractions;

public interface IReferenceNumberService
{
    Task<string> GenerateAsync(string transactionCode, int year, CancellationToken cancellationToken = default);
}

public interface IReferenceNumberSequenceRepository
{
    Task<long> ReserveNextAsync(string prefix, int year, CancellationToken cancellationToken = default);
}

public interface ITransactionRunner
{
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}

public interface IStockReceiptRepository
{
    Task<IReadOnlyList<StockReceipt>> ListAsync(CancellationToken cancellationToken = default);
    Task<StockReceipt?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(StockReceipt receipt, CancellationToken cancellationToken = default);
    void RemoveItems(IEnumerable<StockReceiptItem> items);
    Task<bool> HasOverlappingReceiptRangeAsync(Guid formTypeId, int seriesYear, long beginning, long ending, Guid? excludingReceiptId, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingBookletRangeAsync(Guid formTypeId, int seriesYear, long beginning, long ending, CancellationToken cancellationToken = default);
    Task AddBookletsAsync(IEnumerable<AccountableFormBooklet> booklets, CancellationToken cancellationToken = default);
    Task AddMovementsAsync(IEnumerable<StockMovement> movements, CancellationToken cancellationToken = default);
}

public interface IInventoryRepository
{
    Task<IReadOnlyList<AccountableFormBooklet>> ListAsync(InventoryFilter filter, CancellationToken cancellationToken = default);
    Task<AccountableFormBooklet?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockMovement>> ListMovementsAsync(MovementHistoryFilter filter, CancellationToken cancellationToken = default);
}

public interface IStockReceivingService
{
    Task<IReadOnlyList<StockReceiptDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<StockReceiptDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StockReceiptDto> SaveDraftAsync(SaveStockReceiptDraftRequest request, CancellationToken cancellationToken = default);
    Task SubmitAsync(Guid id, CancellationToken cancellationToken = default);
    Task FinalizeAsync(Guid id, CancellationToken cancellationToken = default);
    Task ReverseAsync(Guid id, string reason, CancellationToken cancellationToken = default);
}

public interface IInventoryService
{
    Task<IReadOnlyList<InventoryBookletDto>> SearchAsync(InventoryFilter filter, CancellationToken cancellationToken = default);
    Task<InventoryDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockMovementDto>> SearchMovementsAsync(MovementHistoryFilter filter, CancellationToken cancellationToken = default);
}

public interface IStockLookupService
{
    Task<IReadOnlyList<StockFormTypeLookup>> ListFormTypesAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockLookup>> ListCollectorsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockLookup>> ListBarangaysAsync(CancellationToken cancellationToken = default);
}
