using AFMS.Application.Models;

namespace AFMS.Application.Validation;

public static class MasterDataValidator
{
    public static IReadOnlyList<string> Validate(SaveAccountableFormTypeRequest request)
    {
        var errors = new List<string>();
        Required(errors, request.Code, "Form code");
        Required(errors, request.Name, "Form name");
        if (request.ReceiptsPerBooklet <= 0) errors.Add("Receipts per booklet must be greater than zero.");
        if (request.SerialNumberLength is <= 0) errors.Add("Serial number length must be greater than zero when provided.");
        return errors;
    }

    public static IReadOnlyList<string> Validate(SaveBarangayRequest request)
    {
        var errors = new List<string>();
        Required(errors, request.Code, "Barangay code");
        Required(errors, request.Name, "Barangay name");
        Required(errors, request.Municipality, "Municipality");
        Required(errors, request.Province, "Province");
        return errors;
    }

    public static IReadOnlyList<string> Validate(SaveCollectorRequest request)
    {
        var errors = new List<string>();
        Required(errors, request.CollectorNumber, "Collector number");
        Required(errors, request.FullName, "Full name");
        if (!string.IsNullOrWhiteSpace(request.EmailAddress) && !request.EmailAddress.Contains('@', StringComparison.Ordinal))
        {
            errors.Add("Email address is not valid.");
        }
        return errors;
    }

    private static void Required(ICollection<string> errors, string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{field} is required.");
    }
}
