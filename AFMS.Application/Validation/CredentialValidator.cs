namespace AFMS.Application.Validation;

public static class CredentialValidator
{
    public static IReadOnlyList<string> ValidateUsername(string username)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3)
        {
            errors.Add("Username must contain at least 3 characters.");
        }

        if (username.Any(character => !char.IsLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
        {
            errors.Add("Username may contain only letters, numbers, periods, underscores, and hyphens.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidatePassword(string password)
    {
        var errors = new List<string>();
        if (password.Length < 12)
        {
            errors.Add("Password must contain at least 12 characters.");
        }

        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
        {
            errors.Add("Password must contain uppercase, lowercase, and numeric characters.");
        }

        if (!password.Any(character => !char.IsLetterOrDigit(character)))
        {
            errors.Add("Password must contain at least one symbol.");
        }

        return errors;
    }
}
