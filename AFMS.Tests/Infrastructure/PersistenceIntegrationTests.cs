using AFMS.Domain.Entities;
using AFMS.Infrastructure.Persistence;
using AFMS.Infrastructure.Security;
using AFMS.Infrastructure;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AFMS.Tests.Infrastructure;

public sealed class PersistenceIntegrationTests
{
    [Fact]
    public async Task Migration_CreatesSchema_AndPasswordIsStoredOnlyAsBCryptHash()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"afms-test-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AfmsDbContext>()
                .UseSqlite($"Data Source={databasePath};Pooling=False")
                .Options;

            await using (var context = new AfmsDbContext(options))
            {
                await context.Database.MigrateAsync();
                Assert.True(await context.Database.CanConnectAsync());

                var hasher = new BCryptPasswordHasher();
                const string password = "Treasury#Secure123";
                var user = new User
                {
                    Username = "integration.admin",
                    DisplayName = "Integration Administrator",
                    PasswordHash = hasher.Hash(password)
                };
                context.Users.Add(user);
                await context.SaveChangesAsync();

                Assert.NotEqual(password, user.PasswordHash);
                Assert.StartsWith("$2", user.PasswordHash, StringComparison.Ordinal);
                Assert.True(hasher.Verify(password, user.PasswordHash));
            }

            await using (var verificationContext = new AfmsDbContext(options))
            {
                var storedUser = await verificationContext.Users.SingleAsync();
                Assert.NotEqual("Treasury#Secure123", storedUser.PasswordHash);
            }
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task Initializer_UpgradesExistingPhase1Roles_AndSeedsPermissions()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"afms-upgrade-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddInfrastructure($"Data Source={databasePath};Pooling=False");
            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AfmsDbContext>();

            await context.GetService<IMigrator>().MigrateAsync("20260715040637_InitialCreate");
            context.Roles.AddRange(
                new Role { Name = "Administrator", Description = "Existing administrator" },
                new Role { Name = "TreasuryStaff", Description = "Legacy role" });
            await context.SaveChangesAsync();

            await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();

            var administrator = await context.Roles.Include(role => role.Permissions).SingleAsync(role => role.Name == "Administrator");
            Assert.Equal(Enum.GetValues<AFMS.Domain.Enums.SystemPermission>().Length, administrator.Permissions.Count);
            Assert.False((await context.Roles.SingleAsync(role => role.Name == "TreasuryStaff")).IsActive);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task Initializer_LoadsConfiguredMasterData_Once_AndPreservesUserEdits()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"afms-seed-{Guid.NewGuid():N}.db");
        try
        {
            var seed = new InitialDataSet
            {
                Version = "test-seed-1",
                SystemSettings = new Dictionary<string, string>
                {
                    ["MunicipalityName"] = "Dipaculao",
                    ["OfficeAddress"] = string.Empty
                },
                Barangays = [new("001", "North Poblacion", "Dipaculao", "Aurora")],
                AccountableFormTypes = [new("AF51-LOCAL", "AF NO. 51 LOCAL", 50)],
                Collectors = [new("SHELLA MAY I. VELASCO", "SHELLA MAY I. VELASCO")]
            };

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddInfrastructure($"Data Source={databasePath};Pooling=False");
            services.AddSingleton<IInitialDataProvider>(new StaticInitialDataProvider(seed));
            await using var provider = services.BuildServiceProvider();

            using (var scope = provider.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
                var context = scope.ServiceProvider.GetRequiredService<AfmsDbContext>();

                Assert.Equal("Dipaculao", (await context.SystemSettings.SingleAsync(item => item.Key == "MunicipalityName")).Value);
                Assert.Equal(string.Empty, (await context.SystemSettings.SingleAsync(item => item.Key == "OfficeAddress")).Value);
                Assert.Equal(50, (await context.AccountableFormTypes.SingleAsync()).DefaultReceiptsPerBooklet);
                Assert.Null((await context.Collectors.SingleAsync()).BarangayId);

                var municipality = await context.SystemSettings.SingleAsync(item => item.Key == "MunicipalityName");
                municipality.Value = "Edited municipality";
                await context.SaveChangesAsync();
            }

            using (var scope = provider.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
                var context = scope.ServiceProvider.GetRequiredService<AfmsDbContext>();

                Assert.Equal(1, await context.Barangays.CountAsync());
                Assert.Equal(1, await context.AccountableFormTypes.CountAsync());
                Assert.Equal(1, await context.Collectors.CountAsync());
                Assert.Equal(1, await context.AuditLogs.CountAsync(item => item.EntityName == "InitialData"));
                Assert.Equal("Edited municipality", (await context.SystemSettings.SingleAsync(item => item.Key == "MunicipalityName")).Value);
            }

            seed.Version = "test-seed-2";
            seed.AccountableFormTypes[0] = new InitialFormType("AF51", "LOCAL", 50, PreviousCode: "AF51-LOCAL");
            seed.Collectors[0] = new InitialCollector("SMV", "SHELLA MAY I. VELASCO");

            using (var scope = provider.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
                var context = scope.ServiceProvider.GetRequiredService<AfmsDbContext>();
                var form = await context.AccountableFormTypes.SingleAsync();
                var collector = await context.Collectors.SingleAsync();
                Assert.Equal("AF51", form.Code);
                Assert.Equal("LOCAL", form.Name);
                Assert.Equal("SMV", collector.Code);
                Assert.Equal(2, await context.AuditLogs.CountAsync(item => item.EntityName == "InitialData"));
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    private sealed class StaticInitialDataProvider(InitialDataSet data) : IInitialDataProvider
    {
        public Task<InitialDataSet?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<InitialDataSet?>(data);
    }
}
