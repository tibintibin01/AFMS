using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AFMS.Core.Persistence;

/// <summary>Lets `dotnet ef migrations add` build the context without starting the app.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new InventoryDbContext(options);
    }
}
