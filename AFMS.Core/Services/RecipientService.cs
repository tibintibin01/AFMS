using AFMS.Core.Domain;
using AFMS.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Core.Services;

public sealed class RecipientService(
    InventoryDbContext context,
    ICurrentUser session,
    IAuditService audit) : IRecipientService
{
    private readonly StockQueries _stock = new(context);

    public async Task<IReadOnlyList<RecipientDto>> ListAsync(
        string? search = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        session.Require();

        var query = context.Recipients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(person =>
                EF.Functions.Like(person.Code, $"%{term}%") ||
                EF.Functions.Like(person.Name, $"%{term}%") ||
                EF.Functions.Like(person.Office ?? string.Empty, $"%{term}%") ||
                EF.Functions.Like(person.Position ?? string.Empty, $"%{term}%"));
        }

        if (isActive is { } active)
            query = query.Where(person => person.IsActive == active);

        var people = await query.OrderBy(person => person.Name).ToListAsync(cancellationToken);
        var totals = await _stock.GetAccountabilityTotalsAsync(null, cancellationToken);

        return people.Select(person =>
        {
            var outstanding = totals
                .Where(entry => entry.Key.RecipientId == person.Id)
                .Sum(entry => Math.Max(0, entry.Value.Released - entry.Value.Returned));
            return Map(person, outstanding);
        }).ToList();
    }

    public async Task<RecipientDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        session.Require();
        var person = await context.Recipients.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (person is null) return null;

        var totals = await _stock.GetAccountabilityTotalsAsync(id, cancellationToken);
        var outstanding = totals.Sum(entry => Math.Max(0, entry.Value.Released - entry.Value.Returned));
        return Map(person, outstanding);
    }

    public async Task<IReadOnlyList<RecipientOption>> ListOptionsAsync(CancellationToken cancellationToken = default)
    {
        session.Require();
        return await context.Recipients.AsNoTracking()
            .Where(person => person.IsActive)
            .OrderBy(person => person.Name)
            .Select(person => new RecipientOption(person.Id, person.Code, person.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<RecipientDto> SaveAsync(SaveRecipientRequest request, CancellationToken cancellationToken = default)
    {
        session.Require();

        var errors = new ErrorList();
        errors.AddIf(string.IsNullOrWhiteSpace(request.Code), "A short code or initials is required.");
        errors.AddIf(string.IsNullOrWhiteSpace(request.Name), "Full name is required.");
        errors.ThrowIfAny();

        var code = request.Code.Trim();
        var duplicate = await context.Recipients.AnyAsync(
            person => person.Code == code && (request.Id == null || person.Id != request.Id),
            cancellationToken);
        if (duplicate)
            throw new ValidationException($"Code '{code}' is already used by another person.");

        Recipient person;
        if (request.Id is { } id)
        {
            person = await context.Recipients.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException("That person is no longer on file.");

            person.Code = code;
            person.Name = request.Name.Trim();
            person.Position = Clean(request.Position);
            person.Office = Clean(request.Office);
            person.ContactNumber = Clean(request.ContactNumber);
            person.Remarks = Clean(request.Remarks);
            person.IsActive = request.IsActive;

            await audit.RecordAsync(ActivityType.RecipientUpdated, $"Record for '{person.Name}' updated.",
                nameof(Recipient), person.Id, person.Code, cancellationToken: cancellationToken);
        }
        else
        {
            person = new Recipient
            {
                Code = code,
                Name = request.Name.Trim(),
                Position = Clean(request.Position),
                Office = Clean(request.Office),
                ContactNumber = Clean(request.ContactNumber),
                Remarks = Clean(request.Remarks),
                IsActive = request.IsActive
            };
            context.Recipients.Add(person);

            await audit.RecordAsync(ActivityType.RecipientCreated, $"'{person.Name}' added to the people list.",
                nameof(Recipient), person.Id, person.Code, cancellationToken: cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        var totals = await _stock.GetAccountabilityTotalsAsync(person.Id, cancellationToken);
        return Map(person, totals.Sum(entry => Math.Max(0, entry.Value.Released - entry.Value.Returned)));
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        session.Require();
        var person = await context.Recipients.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("That person is no longer on file.");

        if (person.IsActive == isActive) return;

        // Hiding someone who still holds stock would make their accountability disappear
        // from the pick lists while the items are still out.
        if (!isActive)
        {
            var totals = await _stock.GetAccountabilityTotalsAsync(id, cancellationToken);
            var outstanding = totals.Sum(entry => Math.Max(0, entry.Value.Released - entry.Value.Returned));
            if (outstanding > 0)
                throw new ValidationException(
                    $"{person.Name} still holds {outstanding} item(s). Record the returns first, then hide this person.");
        }

        person.IsActive = isActive;
        await audit.RecordAsync(
            ActivityType.RecipientStatusChanged,
            $"'{person.Name}' was {(isActive ? "reactivated" : "hidden from new releases")}.",
            nameof(Recipient), person.Id, person.Code, cancellationToken: cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static RecipientDto Map(Recipient person, long outstanding) => new(
        person.Id, person.Code, person.Name, person.Position, person.Office,
        person.ContactNumber, person.Remarks, person.IsActive, outstanding);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
