using AFMS.Domain.Entities;
using AFMS.Infrastructure.Persistence;
using AFMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

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
}
