using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Application.Validation;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;

namespace AFMS.Application.Services;

public sealed class FirstRunService(
    IUserRepository users,
    IRoleRepository roles,
    IAuditLogRepository auditLogs,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork) : IFirstRunService
{
    public Task<bool> RequiresAdministratorSetupAsync(CancellationToken cancellationToken = default) =>
        RequiresSetupAsync(cancellationToken);

    public async Task<AuthenticationResult> CreateAdministratorAsync(
        CreateAdministratorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await users.AnyAsync(cancellationToken))
        {
            return AuthenticationResult.Failure("Initial administrator setup has already been completed.");
        }

        var errors = CredentialValidator.ValidateUsername(request.Username)
            .Concat(CredentialValidator.ValidatePassword(request.Password))
            .ToList();
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors.Add("Display name is required.");
        }

        if (errors.Count > 0)
        {
            return AuthenticationResult.Failure(string.Join(Environment.NewLine, errors));
        }

        var administratorRole = await roles.GetByNameAsync("Administrator", cancellationToken);
        if (administratorRole is null)
        {
            throw new InvalidOperationException("The Administrator role was not initialized.");
        }

        var user = new User
        {
            Username = request.Username.Trim(),
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = passwordHasher.Hash(request.Password)
        };
        user.UserRoles.Add(new UserRole { User = user, Role = administratorRole });
        await users.AddAsync(user, cancellationToken);
        await auditLogs.AddAsync(new AuditLog
        {
            User = user,
            ActivityType = UserActivityType.AccountCreated,
            Description = "Initial administrator account created.",
            MachineName = Environment.MachineName
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return AuthenticationResult.Success();
    }

    private async Task<bool> RequiresSetupAsync(CancellationToken cancellationToken) =>
        !await users.AnyAsync(cancellationToken);
}
