using AFMS.Application.Models;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;

namespace AFMS.Application.Abstractions;

public interface IOperationalTransactionRepository
{
    Task<IReadOnlyList<AccountabilityRange>> ListOwnedRangesAsync(Guid? collectorId, Guid? formTypeId, CancellationToken cancellationToken = default);
    Task<AccountableFormBooklet?> GetBookletAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Issuance?> FindSourceIssuanceAsync(Guid bookletId, Guid collectorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UsageReport>> ListUsageAsync(CancellationToken cancellationToken = default);
    Task<UsageReport?> GetUsageAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transfer>> ListTransfersAsync(CancellationToken cancellationToken = default);
    Task<Transfer?> GetTransferAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReturnTransaction>> ListReturnsAsync(CancellationToken cancellationToken = default);
    Task<ReturnTransaction?> GetReturnAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FormException>> ListExceptionsAsync(CancellationToken cancellationToken = default);
    Task<FormException?> GetExceptionAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class;
    void RemoveRange(IEnumerable<UsageRange> ranges);
    Task AddMovementsAsync(IEnumerable<StockMovement> movements, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransactionAttachment>> ListAttachmentsAsync(AttachmentTransactionType type, Guid transactionId, CancellationToken cancellationToken = default);
}

public interface IOperationalTransactionService
{
    Task<IReadOnlyList<OwnedRangeDto>> ListOwnedRangesAsync(Guid? collectorId = null, Guid? formTypeId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UsageReportDto>> ListUsageAsync(CancellationToken cancellationToken = default);
    Task<UsageReportDto> SaveUsageDraftAsync(SaveUsageReportRequest request, CancellationToken cancellationToken = default);
    Task FinalizeUsageAsync(Guid id, CancellationToken cancellationToken = default);
    Task ReverseUsageAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransferDto>> ListTransfersAsync(CancellationToken cancellationToken = default);
    Task<TransferDto> SaveTransferDraftAsync(SaveTransferRequest request, CancellationToken cancellationToken = default);
    Task SubmitTransferAsync(Guid id, CancellationToken cancellationToken = default);
    Task ApproveTransferAsync(Guid id, CancellationToken cancellationToken = default);
    Task RejectTransferAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task FinalizeTransferAsync(Guid id, CancellationToken cancellationToken = default);
    Task ReverseTransferAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReturnDto>> ListReturnsAsync(CancellationToken cancellationToken = default);
    Task<ReturnDto> SaveReturnDraftAsync(SaveReturnRequest request, CancellationToken cancellationToken = default);
    Task SubmitReturnAsync(Guid id, CancellationToken cancellationToken = default);
    Task ApproveReturnAsync(Guid id, CancellationToken cancellationToken = default);
    Task RejectReturnAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task FinalizeReturnAsync(Guid id, CancellationToken cancellationToken = default);
    Task ReverseReturnAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FormExceptionDto>> ListExceptionsAsync(CancellationToken cancellationToken = default);
    Task<FormExceptionDto> SaveExceptionDraftAsync(SaveExceptionRequest request, CancellationToken cancellationToken = default);
    Task SubmitExceptionAsync(Guid id, CancellationToken cancellationToken = default);
    Task ApproveExceptionAsync(Guid id, CancellationToken cancellationToken = default);
    Task RejectExceptionAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task FinalizeExceptionAsync(Guid id, CancellationToken cancellationToken = default);
    Task ReverseExceptionAsync(Guid id, string reason, CancellationToken cancellationToken = default);
}

public interface ITransactionAttachmentService
{
    Task<AttachmentDto> AddAsync(AddAttachmentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttachmentDto>> ListAsync(AttachmentTransactionType type, Guid transactionId, CancellationToken cancellationToken = default);
}
