using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LiveFuelMap.DAL.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LiveFuelMapDbContext>
{
    public LiveFuelMapDbContext CreateDbContext(string[] args)
    {
        var connectionString = ConnectionStringFactory.Resolve(basePath: Directory.GetCurrentDirectory());

        var options = new DbContextOptionsBuilder<LiveFuelMapDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 41)))
            .Options;

        return new LiveFuelMapDbContext(options);
    }
}
