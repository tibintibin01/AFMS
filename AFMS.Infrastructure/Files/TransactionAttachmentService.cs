using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Application.Validation;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;

namespace AFMS.Infrastructure.Files;

public sealed class TransactionAttachmentService(IApplicationPathService paths, IOperationalTransactionRepository repository,
    IUnitOfWork unitOfWork, IAuthorizationService authorization, ICurrentUserSession session, IAuditService audit,
    IDatabaseOperationCoordinator coordinator) : ITransactionAttachmentService
{
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".exe", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jse", ".msi", ".scr", ".dll", ".jar", ".hta" };

    public async Task<AttachmentDto> AddAsync(AddAttachmentRequest request, CancellationToken cancellationToken = default)
    {
        var permission = request.TransactionType switch { AttachmentTransactionType.Transfer => SystemPermission.TransferStock,
            AttachmentTransactionType.Return => SystemPermission.ReturnStock, _ => SystemPermission.ManageExceptions };
        authorization.EnsurePermission(permission);
        await using var writeLock = await coordinator.EnterWriteAsync(cancellationToken);
        if (!File.Exists(request.SourcePath)) throw new FileNotFoundException("The attachment file was not found.", request.SourcePath);
        var info = new FileInfo(request.SourcePath); var extension = info.Extension;
        if (info.Length <= 0 || info.Length > 25 * 1024 * 1024) throw new ApplicationValidationException(["Attachments must be between 1 byte and 25 MB."]);
        if (BlockedExtensions.Contains(extension)) throw new ApplicationValidationException(["Executable or script attachments are not permitted."]);
        var user = session.User ?? throw new UnauthorizedAccessException("A signed-in user is required.");
        var folder = Path.Combine(paths.DataDirectory, "Attachments", request.TransactionType.ToString(), request.TransactionId.ToString("N"));
        Directory.CreateDirectory(folder); var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}"; var target = Path.Combine(folder, storedName);
        File.Copy(info.FullName, target, false);
        try
        {
            var entity = new TransactionAttachment { OriginalFileName = info.Name, StoredFileName = Path.GetRelativePath(paths.DataDirectory, target),
                FileType = string.IsNullOrWhiteSpace(extension) ? "application/octet-stream" : extension.TrimStart('.').ToLowerInvariant(), FileSize = info.Length,
                UploadedByUserId = user.Id, RelatedTransactionType = request.TransactionType, RelatedTransactionId = request.TransactionId };
            await repository.AddAsync(entity, cancellationToken); await audit.RecordAsync(UserActivityType.RecordCreated, $"Attachment {info.Name} added.", nameof(TransactionAttachment), entity.Id, null, new { entity.RelatedTransactionType, entity.RelatedTransactionId, entity.OriginalFileName }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken); return Map(entity);
        }
        catch { if (File.Exists(target)) File.Delete(target); throw; }
    }

    public async Task<IReadOnlyList<AttachmentDto>> ListAsync(AttachmentTransactionType type, Guid transactionId, CancellationToken cancellationToken = default) =>
        (await repository.ListAttachmentsAsync(type, transactionId, cancellationToken)).Select(Map).ToList();
    private static AttachmentDto Map(TransactionAttachment x) => new(x.Id, x.OriginalFileName, x.FileType, x.FileSize, x.UploadDate);
}
