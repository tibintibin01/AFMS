namespace AFMS.Core;

/// <summary>
/// Raised when input fails a business rule. Every message is written for the
/// person using the app, so the UI can show them without translation.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(IReadOnlyList<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors.Count > 0 ? errors : ["The information provided is not valid."];
    }

    public ValidationException(string error) : this([error])
    {
    }

    public IReadOnlyList<string> Errors { get; }
}

/// <summary>Collects validation messages so a form can report every problem at once.</summary>
public sealed class ErrorList
{
    private readonly List<string> _errors = [];

    public bool HasErrors => _errors.Count > 0;

    public void Add(string message) => _errors.Add(message);

    public void AddIf(bool condition, string message)
    {
        if (condition) _errors.Add(message);
    }

    public void AddRange(IEnumerable<string> messages) => _errors.AddRange(messages);

    public void ThrowIfAny()
    {
        if (_errors.Count > 0) throw new ValidationException(_errors);
    }
}

/// <summary>Password and username rules for sign-in accounts.</summary>
public static class CredentialRules
{
    public const int MinimumPasswordLength = 12;

    public static IReadOnlyList<string> ValidateUsername(string? username)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(username))
        {
            errors.Add("Username is required.");
            return errors;
        }

        var trimmed = username.Trim();
        if (trimmed.Length < 3)
            errors.Add("Username must be at least 3 characters.");
        if (trimmed.Any(character => !char.IsLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
            errors.Add("Username may contain only letters, numbers, periods, underscores, and hyphens.");

        return errors;
    }

    public static IReadOnlyList<string> ValidatePassword(string? password)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Password is required.");
            return errors;
        }

        if (password.Length < MinimumPasswordLength)
            errors.Add($"Password must be at least {MinimumPasswordLength} characters.");
        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
            errors.Add("Password must include an uppercase letter, a lowercase letter, and a number.");
        if (!password.Any(character => !char.IsLetterOrDigit(character)))
            errors.Add("Password must include at least one symbol.");

        return errors;
    }
}
