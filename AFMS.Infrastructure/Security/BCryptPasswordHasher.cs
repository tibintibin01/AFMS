using AFMS.Application.Abstractions;

namespace AFMS.Infrastructure.Security;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
