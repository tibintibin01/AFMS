using AFMS.Application.Models;
using AFMS.Domain.Entities;

namespace AFMS.Application.Abstractions;

public interface IIssuanceRepository
{
    Task<IReadOnlyList<Issuance>> ListAsync(CancellationToken cancellationToken = default);
    Task<Issuance?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Issuance issuance, CancellationToken cancellationToken = default);
    void RemoveItems(IEnumerable<IssuanceItem> items);
    Task<IReadOnlyList<AccountabilityRange>> ListAvailableBookletsAsync(AvailableBookletFilter filter, Guid? excludingIssuanceId, CancellationToken cancellationToken = default);
    Task<bool> IsRangeIssuableAsync(Guid bookletId, long beginningSerial, long endingSerial, Guid? excludingIssuanceId, CancellationToken cancellationToken = default);
    Task AddMovementsAsync(IEnumerable<StockMovement> movements, CancellationToken cancellationToken = default);
    Task AddAccountabilityRangeAsync(AccountabilityRange range, CancellationToken cancellationToken = default);
}

public interface ICollectorAccountabilityRepository
{
    Task<IReadOnlyList<StockMovement>> ListCollectorMovementsAsync(Guid collectorId, Guid? formTypeId, Guid? barangayId, bool outstandingOnly, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountableFormBooklet>> ListCollectorBookletsAsync(Guid collectorId, CancellationToken cancellationToken = default);
}

public interface IIssuanceService
{
    Task<IReadOnlyList<IssuanceDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<IssuanceDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AvailableBookletDto>> ListAvailableBookletsAsync(AvailableBookletFilter filter, Guid? excludingIssuanceId = null, CancellationToken cancellationToken = default);
    Task<IssuanceDto> SaveDraftAsync(SaveIssuanceDraftRequest request, CancellationToken cancellationToken = default);
    Task SubmitAsync(Guid id, CancellationToken cancellationToken = default);
    Task ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task RejectAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task FinalizeAsync(Guid id, CancellationToken cancellationToken = default);
    Task ReverseAsync(Guid id, string reason, CancellationToken cancellationToken = default);
}

public interface ICollectorAccountabilityService
{
    Task<IReadOnlyList<CollectorLedgerEntryDto>> GetLedgerAsync(CollectorLedgerFilter filter, CancellationToken cancellationToken = default);
    Task<CollectorAccountabilityDetailDto?> GetCollectorDetailAsync(Guid collectorId, CancellationToken cancellationToken = default);
}

public interface IIssuanceReceiptService
{
    Task<string> GeneratePdfAsync(Guid issuanceId, string? outputFolder = null, bool forPrintPreview = false, CancellationToken cancellationToken = default);
}

public interface IIssuancePdfWriter
{
    Task WriteAsync(IssuanceReceiptDocument document, string outputPath, CancellationToken cancellationToken = default);
}

public interface IReportSettingsReader
{
    Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken = default);
}
