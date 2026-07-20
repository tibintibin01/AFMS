using AFMS.Application;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Application.Services;
using AFMS.Application.Validation;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;
using AFMS.Infrastructure;
using AFMS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFMS.Tests.Application;

public sealed class Phase2AdministrationTests
{
    [Fact]
    public async Task AccountableFormType_RejectsDuplicateCode()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var service = environment.GetRequiredService<IAccountableFormTypeService>();
        await service.SaveAsync(FormRequest("AF-51", "Official Receipt"));

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => service.SaveAsync(FormRequest("af-51", "Another Form")));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Barangay_RejectsDuplicateCodeAndName()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var service = environment.GetRequiredService<IBarangayService>();
        await service.SaveAsync(BarangayRequest("001", "Poblacion"));

        var codeException = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => service.SaveAsync(BarangayRequest("001", "San Roque")));
        var nameException = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => service.SaveAsync(BarangayRequest("002", "poblacion")));

        Assert.Contains("code already exists", codeException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name already exists", nameException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Collector_RejectsDuplicateCollectorNumber()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var service = environment.GetRequiredService<ICollectorService>();
        await service.SaveAsync(CollectorRequest("COL-001", "First Collector"));

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => service.SaveAsync(CollectorRequest("col-001", "Second Collector")));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PermissionService_AllowsGrantedPermissionAndRejectsMissingPermission()
    {
        var session = new CurrentUserSession();
        session.Begin(new CurrentUser(Guid.NewGuid(), "viewer", "Viewer", new HashSet<string> { "Auditor or Viewer" },
            new HashSet<SystemPermission> { SystemPermission.ViewDashboard }, false));
        var service = new AuthorizationService(session);

        Assert.True(service.HasPermission(SystemPermission.ViewDashboard));
        Assert.False(service.HasPermission(SystemPermission.ManageUsers));
        Assert.Throws<UnauthorizedAccessException>(() => service.EnsurePermission(SystemPermission.ManageUsers));
    }

    [Fact]
    public async Task Authentication_RejectsInactiveUser()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var administration = environment.GetRequiredService<IUserAdministrationService>();
        var role = (await administration.ListRolesAsync()).Single(item => item.Name == "Encoder");
        await administration.CreateAsync(new CreateUserRequest("inactive.user", "Inactive User", null, role.Id, "Inactive#User123", true));
        var user = (await administration.ListAsync("inactive.user", null)).Single();
        await administration.SetActiveAsync(user.Id, false);

        var authentication = environment.GetRequiredService<IAuthenticationService>();
        await authentication.LogoutAsync();
        var result = await authentication.LoginAsync(new LoginRequest("inactive.user", "Inactive#User123"));

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid username or password.", result.ErrorMessage);
    }

    [Fact]
    public async Task ForcedPasswordChange_MustBeCompletedBeforeSessionIsCleared()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var administration = environment.GetRequiredService<IUserAdministrationService>();
        var role = (await administration.ListRolesAsync()).Single(item => item.Name == "Encoder");
        await administration.CreateAsync(new CreateUserRequest("forced.user", "Forced User", null, role.Id, "Temporary#Pass123", true));
        var authentication = environment.GetRequiredService<IAuthenticationService>();
        await authentication.LogoutAsync();

        Assert.True((await authentication.LoginAsync(new LoginRequest("forced.user", "Temporary#Pass123"))).Succeeded);
        Assert.True(environment.GetRequiredService<ICurrentUserSession>().User?.MustChangePassword);
        Assert.True((await authentication.ChangePasswordAsync("Temporary#Pass123", "Permanent#Pass456")).Succeeded);
        Assert.False(environment.GetRequiredService<ICurrentUserSession>().User?.MustChangePassword);

        await authentication.LogoutAsync();
        Assert.False((await authentication.LoginAsync(new LoginRequest("forced.user", "Temporary#Pass123"))).Succeeded);
        Assert.True((await authentication.LoginAsync(new LoginRequest("forced.user", "Permanent#Pass456"))).Succeeded);
    }

    [Fact]
    public async Task LastActiveAdministrator_CannotBeDemotedByAnotherPrivilegedSession()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var administration = environment.GetRequiredService<IUserAdministrationService>();
        var admin = (await administration.ListAsync("admin", true)).Single();
        var encoder = (await administration.ListRolesAsync()).Single(role => role.Name == "Encoder");
        environment.GetRequiredService<ICurrentUserSession>().Begin(new CurrentUser(Guid.NewGuid(), "security.reviewer", "Security Reviewer",
            new HashSet<string> { "Administrator" }, new HashSet<SystemPermission> { SystemPermission.ManageUsers }, false));

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(() => administration.UpdateAsync(
            new(admin.Id, admin.FullName, null, encoder.Id, true, false)));

        Assert.Contains("at least one active Administrator", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DatabaseGuard_RejectsDeactivatingLastAdministratorWhenServiceIsBypassed()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var context = environment.GetRequiredService<AfmsDbContext>();
        var administrator = await context.Users.SingleAsync(user => user.Username == "admin");
        administrator.IsActive = false;

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("at least one active Administrator", exception.InnerException?.Message ?? exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PasswordReset_InvalidatesPreviouslyIssuedSessionOnRefresh()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var administration = environment.GetRequiredService<IUserAdministrationService>();
        var encoder = (await administration.ListRolesAsync()).Single(role => role.Name == "Encoder");
        await administration.CreateAsync(new CreateUserRequest("reset.user", "Reset User", null, encoder.Id, "Temporary#Pass123", false));
        var user = (await administration.ListAsync("reset.user", true)).Single();
        var authentication = environment.GetRequiredService<IAuthenticationService>();
        await authentication.LogoutAsync();
        Assert.True((await authentication.LoginAsync(new LoginRequest("reset.user", "Temporary#Pass123"))).Succeeded);
        var staleSession = environment.GetRequiredService<ICurrentUserSession>().User!;

        await authentication.LogoutAsync();
        Assert.True((await authentication.LoginAsync(new LoginRequest("admin", "Administrator#123"))).Succeeded);
        await administration.ResetPasswordAsync(new(user.Id, "Replacement#Pass456", true));
        environment.GetRequiredService<ICurrentUserSession>().Begin(staleSession);

        Assert.False(await authentication.RefreshCurrentUserAsync());
        Assert.False(environment.GetRequiredService<ICurrentUserSession>().IsAuthenticated);
    }

    [Fact]
    public async Task StartupRoleReconciliation_DeactivatesPermissionsRemovedFromPolicy()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var context = environment.GetRequiredService<AfmsDbContext>();
        var encoder = await context.Roles.Include(role => role.Permissions).SingleAsync(role => role.Name == "Encoder");
        var unauthorized = new RolePermission { RoleId = encoder.Id, Permission = SystemPermission.ManageUsers };
        context.RolePermissions.Add(unauthorized);
        await context.SaveChangesAsync();

        await environment.GetRequiredService<IDatabaseInitializer>().InitializeAsync();

        context.ChangeTracker.Clear();
        var reconciled = await context.RolePermissions.SingleAsync(permission =>
            permission.RoleId == encoder.Id && permission.Permission == SystemPermission.ManageUsers);
        Assert.False(reconciled.IsActive);
    }

    [Fact]
    public void RequiredFieldValidation_ReturnsActionableErrors()
    {
        var formErrors = MasterDataValidator.Validate(FormRequest(string.Empty, string.Empty) with { ReceiptsPerBooklet = 0 });
        var barangayErrors = MasterDataValidator.Validate(BarangayRequest(string.Empty, string.Empty) with { Municipality = "", Province = "" });
        var collectorErrors = MasterDataValidator.Validate(CollectorRequest(string.Empty, string.Empty));

        Assert.Contains(formErrors, error => error.Contains("Form code", StringComparison.Ordinal));
        Assert.Contains(formErrors, error => error.Contains("Form name", StringComparison.Ordinal));
        Assert.Contains(formErrors, error => error.Contains("greater than zero", StringComparison.Ordinal));
        Assert.Equal(4, barangayErrors.Count);
        Assert.Contains(collectorErrors, error => error.Contains("Collector number", StringComparison.Ordinal));
        Assert.Contains(collectorErrors, error => error.Contains("Full name", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MasterData_EditStatusSettingsAndAudit_WorkEndToEnd()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var formTypes = environment.GetRequiredService<IAccountableFormTypeService>();
        var created = await formTypes.SaveAsync(FormRequest("AF-CRUD", "Original Name"));
        var updated = await formTypes.SaveAsync(FormRequest("AF-CRUD", "Updated Name") with { Id = created.Id, Remarks = "Edited" });
        Assert.Equal("Updated Name", updated.Name);

        await formTypes.SetActiveAsync(created.Id, false);
        Assert.False((await formTypes.ListAsync("AF-CRUD", false)).Single().IsActive);
        await formTypes.SetActiveAsync(created.Id, true);
        Assert.True((await formTypes.ListAsync("AF-CRUD", true)).Single().IsActive);

        var settingsService = environment.GetRequiredService<ISystemSettingsService>();
        var settings = await settingsService.GetAsync();
        settings.MunicipalityName = "Configurable Municipality";
        settings.PreparedBy = "Authorized Preparer";
        await settingsService.SaveAsync(settings);
        var reloadedSettings = await settingsService.GetAsync();
        Assert.Equal("Configurable Municipality", reloadedSettings.MunicipalityName);
        Assert.Equal("Authorized Preparer", reloadedSettings.PreparedBy);

        var audit = await environment.GetRequiredService<IAuditQueryService>().ListAsync();
        Assert.Contains(audit, entry => entry.EntityName == "AccountableFormType" && entry.ActivityType == UserActivityType.RecordUpdated);
        Assert.Contains(audit, entry => entry.ActivityType == UserActivityType.Deactivated);
        Assert.Contains(audit, entry => entry.ActivityType == UserActivityType.SystemSettingChanged);
    }

    private static SaveAccountableFormTypeRequest FormRequest(string code, string name) =>
        new(null, code, name, null, "Receipt", 50, null, 8, true, true, null);
    private static SaveBarangayRequest BarangayRequest(string code, string name) =>
        new(null, code, name, "Sample Municipality", "Sample Province", true, null);
    private static SaveCollectorRequest CollectorRequest(string number, string name) =>
        new(null, number, name, null, "Collector", "Treasury", null, null, null, null, true, null);

    private sealed class TestEnvironment : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;
        private readonly string _databasePath;

        private TestEnvironment(ServiceProvider provider, IServiceScope scope, string databasePath)
        {
            _provider = provider; _scope = scope; _databasePath = databasePath;
        }

        public T GetRequiredService<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

        public static async Task<TestEnvironment> CreateAsync()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"afms-phase2-{Guid.NewGuid():N}.db");
            var services = new ServiceCollection();
            services.AddLogging(); services.AddApplication(); services.AddInfrastructure($"Data Source={databasePath};Pooling=False");
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var environment = new TestEnvironment(provider, scope, databasePath);
            await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
            var firstRun = scope.ServiceProvider.GetRequiredService<IFirstRunService>();
            var setup = await firstRun.CreateAdministratorAsync(new CreateAdministratorRequest("admin", "Administrator", "Administrator#123"));
            Assert.True(setup.Succeeded, setup.ErrorMessage);
            var login = await scope.ServiceProvider.GetRequiredService<IAuthenticationService>()
                .LoginAsync(new LoginRequest("admin", "Administrator#123"));
            Assert.True(login.Succeeded, login.ErrorMessage);
            return environment;
        }

        public async ValueTask DisposeAsync()
        {
            _scope.Dispose(); await _provider.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (File.Exists(_databasePath)) File.Delete(_databasePath);
        }
    }
}
