using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AFMS.Infrastructure.Persistence;

public sealed class AfmsDbContextFactory : IDesignTimeDbContextFactory<AfmsDbContext>
{
    public AfmsDbContext CreateDbContext(string[] args)
    {
        var databasePath = Path.Combine(Directory.GetCurrentDirectory(), "afms.design.db");
        var options = new DbContextOptionsBuilder<AfmsDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        return new AfmsDbContext(options);
    }
}
