using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Application.Validation;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;

namespace AFMS.Application.Services;

public sealed class AccountableFormTypeService(
    IAccountableFormTypeRepository repository,
    IAuthorizationService authorization,
    IAuditService audit,
    IUnitOfWork unitOfWork) : IAccountableFormTypeService
{
    public async Task<IReadOnlyList<AccountableFormTypeDto>> ListAsync(string? search, bool? isActive, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageFormTypes);
        var entities = await repository.ListAsync(search, isActive, cancellationToken);
        var results = new List<AccountableFormTypeDto>(entities.Count);
        foreach (var entity in entities)
        {
            results.Add(Map(entity, await repository.HasTransactionHistoryAsync(entity.Id, cancellationToken)));
        }
        return results;
    }

    public async Task<AccountableFormTypeDto> SaveAsync(SaveAccountableFormTypeRequest request, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageFormTypes);
        Validate(MasterDataValidator.Validate(request));
        var code = request.Code.Trim();
        if (await repository.CodeExistsAsync(code.ToUpperInvariant(), request.Id, cancellationToken))
            throw new ApplicationValidationException(["Form code already exists."]);

        var entity = request.Id is null ? new AccountableFormType { Code = code, Name = request.Name.Trim() } :
            await repository.GetAsync(request.Id.Value, cancellationToken) ?? throw new InvalidOperationException("Accountable form type was not found.");
        var oldValues = request.Id is null ? null : Map(entity, false);
        Apply(entity, request);
        if (request.Id is null) await repository.AddAsync(entity, cancellationToken);
        await audit.RecordAsync(request.Id is null ? UserActivityType.RecordCreated : UserActivityType.RecordUpdated,
            request.Id is null ? "Accountable form type created." : "Accountable form type edited.",
            nameof(AccountableFormType), entity.Id, oldValues, Map(entity, false), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(entity, await repository.HasTransactionHistoryAsync(entity.Id, cancellationToken));
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageFormTypes);
        var entity = await repository.GetAsync(id, cancellationToken) ?? throw new InvalidOperationException("Accountable form type was not found.");
        if (entity.IsActive == isActive) return;
        var old = new { entity.IsActive };
        entity.IsActive = isActive;
        await audit.RecordAsync(isActive ? UserActivityType.Activated : UserActivityType.Deactivated,
            isActive ? "Accountable form type reactivated." : "Accountable form type deactivated.",
            nameof(AccountableFormType), entity.Id, old, new { entity.IsActive }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(AccountableFormType entity, SaveAccountableFormTypeRequest request)
    {
        entity.Code = request.Code.Trim(); entity.Name = request.Name.Trim(); entity.Description = Clean(request.Description);
        entity.Category = Clean(request.Category); entity.DefaultReceiptsPerBooklet = request.ReceiptsPerBooklet;
        entity.SerialNumberPrefix = Clean(request.SerialNumberPrefix); entity.SerialNumberLength = request.SerialNumberLength;
        entity.RequiresIndividualReceiptTracking = request.RequiresIndividualReceiptTracking;
        entity.IsActive = request.IsActive; entity.Remarks = Clean(request.Remarks);
    }

    private static AccountableFormTypeDto Map(AccountableFormType entity, bool history) => new(entity.Id, entity.Code, entity.Name,
        entity.Description, entity.Category, entity.DefaultReceiptsPerBooklet, entity.SerialNumberPrefix, entity.SerialNumberLength,
        entity.RequiresIndividualReceiptTracking, entity.IsActive, entity.Remarks, history);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void Validate(IReadOnlyList<string> errors) { if (errors.Count > 0) throw new ApplicationValidationException(errors); }
}

public sealed class BarangayService(
    IBarangayRepository repository,
    IAuthorizationService authorization,
    IAuditService audit,
    IUnitOfWork unitOfWork) : IBarangayService
{
    public async Task<IReadOnlyList<BarangayDto>> ListAsync(string? search, bool? isActive, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageBarangays);
        return (await repository.ListAsync(search, isActive, cancellationToken)).Select(Map).ToList();
    }

    public async Task<BarangayDto> SaveAsync(SaveBarangayRequest request, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageBarangays);
        var errors = MasterDataValidator.Validate(request).ToList();
        if (await repository.CodeExistsAsync(request.Code.Trim().ToUpperInvariant(), request.Id, cancellationToken)) errors.Add("Barangay code already exists.");
        if (await repository.NameExistsAsync(request.Name.Trim().ToUpperInvariant(), request.Id, cancellationToken)) errors.Add("Barangay name already exists.");
        if (errors.Count > 0) throw new ApplicationValidationException(errors);
        var entity = request.Id is null ? new Barangay { Code = request.Code.Trim(), Name = request.Name.Trim(), Municipality = request.Municipality.Trim(), Province = request.Province.Trim() } :
            await repository.GetAsync(request.Id.Value, cancellationToken) ?? throw new InvalidOperationException("Barangay was not found.");
        var oldValues = request.Id is null ? null : Map(entity);
        entity.Code = request.Code.Trim(); entity.Name = request.Name.Trim(); entity.Municipality = request.Municipality.Trim();
        entity.Province = request.Province.Trim(); entity.IsActive = request.IsActive; entity.Remarks = Clean(request.Remarks);
        if (request.Id is null) await repository.AddAsync(entity, cancellationToken);
        await audit.RecordAsync(request.Id is null ? UserActivityType.RecordCreated : UserActivityType.RecordUpdated,
            request.Id is null ? "Barangay created." : "Barangay edited.", nameof(Barangay), entity.Id, oldValues, Map(entity), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageBarangays);
        var entity = await repository.GetAsync(id, cancellationToken) ?? throw new InvalidOperationException("Barangay was not found.");
        if (entity.IsActive == isActive) return;
        var old = new { entity.IsActive }; entity.IsActive = isActive;
        await audit.RecordAsync(isActive ? UserActivityType.Activated : UserActivityType.Deactivated,
            isActive ? "Barangay reactivated." : "Barangay deactivated.", nameof(Barangay), entity.Id, old, new { entity.IsActive }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static BarangayDto Map(Barangay entity) => new(entity.Id, entity.Code, entity.Name, entity.Municipality, entity.Province, entity.IsActive, entity.Remarks);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CollectorService(
    ICollectorRepository repository,
    IAuthorizationService authorization,
    IAuditService audit,
    IUnitOfWork unitOfWork) : ICollectorService
{
    public async Task<IReadOnlyList<CollectorDto>> ListAsync(string? search, Guid? barangayId, bool? isActive, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageCollectors);
        var entities = await repository.ListAsync(search, barangayId, isActive, cancellationToken);
        var results = new List<CollectorDto>(entities.Count);
        foreach (var entity in entities) results.Add(Map(entity, await repository.HasTransactionHistoryAsync(entity.Id, cancellationToken)));
        return results;
    }

    public async Task<CollectorDto> SaveAsync(SaveCollectorRequest request, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageCollectors);
        var errors = MasterDataValidator.Validate(request).ToList();
        if (await repository.NumberExistsAsync(request.CollectorNumber.Trim().ToUpperInvariant(), request.Id, cancellationToken)) errors.Add("Collector number already exists.");
        if (errors.Count > 0) throw new ApplicationValidationException(errors);
        var entity = request.Id is null ? new Collector { Code = request.CollectorNumber.Trim(), FullName = request.FullName.Trim() } :
            await repository.GetAsync(request.Id.Value, cancellationToken) ?? throw new InvalidOperationException("Collector was not found.");
        var oldValues = request.Id is null ? null : Map(entity, false);
        Apply(entity, request);
        if (request.Id is null) await repository.AddAsync(entity, cancellationToken);
        await audit.RecordAsync(request.Id is null ? UserActivityType.RecordCreated : UserActivityType.RecordUpdated,
            request.Id is null ? "Collector created." : "Collector edited.", nameof(Collector), entity.Id, oldValues, Map(entity, false), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(entity, await repository.HasTransactionHistoryAsync(entity.Id, cancellationToken));
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageCollectors);
        var entity = await repository.GetAsync(id, cancellationToken) ?? throw new InvalidOperationException("Collector was not found.");
        if (entity.IsActive == isActive) return;
        var old = new { entity.IsActive }; entity.IsActive = isActive;
        await audit.RecordAsync(isActive ? UserActivityType.Activated : UserActivityType.Deactivated,
            isActive ? "Collector reactivated." : "Collector deactivated.", nameof(Collector), entity.Id, old, new { entity.IsActive }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(Collector entity, SaveCollectorRequest request)
    {
        entity.Code = request.CollectorNumber.Trim(); entity.FullName = request.FullName.Trim(); entity.EmployeeNumber = Clean(request.EmployeeNumber);
        entity.Position = Clean(request.Position); entity.OfficeOrDepartment = Clean(request.OfficeOrDepartment); entity.BarangayId = request.BarangayId;
        entity.ContactNumber = Clean(request.ContactNumber); entity.EmailAddress = Clean(request.EmailAddress);
        entity.AppointmentReference = Clean(request.AppointmentReference); entity.IsActive = request.IsActive; entity.Remarks = Clean(request.Remarks);
    }
    private static CollectorDto Map(Collector entity, bool history) => new(entity.Id, entity.Code, entity.FullName, entity.EmployeeNumber,
        entity.Position, entity.OfficeOrDepartment, entity.BarangayId, entity.Barangay?.Name, entity.ContactNumber, entity.EmailAddress,
        entity.AppointmentReference, entity.IsActive, entity.Remarks, history);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
