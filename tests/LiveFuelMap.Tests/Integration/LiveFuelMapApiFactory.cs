using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.BLL.Services;
using LiveFuelMap.DAL.Entities;
using LiveFuelMap.DAL.Persistence;
using LiveFuelMap.Infrastructure.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace LiveFuelMap.Tests.Integration;

public sealed class LiveFuelMapApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = $"Data Source=file:livefuelmap-tests-{Guid.NewGuid():N}?mode=memory&cache=shared";
    private SqliteConnection? _keeperConnection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<LiveFuelMapDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<LiveFuelMapDbContext>>();
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IEmailSender>();
            services.RemoveAll<IExternalAutomotiveContextService>();
            services.AddSingleton<IEmailSender, NoopEmailSender>();
            services.AddScoped<IExternalAutomotiveContextService, NoopExternalAutomotiveContextService>();

            _keeperConnection = new SqliteConnection(_connectionString);
            _keeperConnection.Open();
            services.AddDbContext<LiveFuelMapDbContext>(options => options.UseSqlite(_connectionString));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
            db.Database.EnsureCreated();

            if (!db.FuelPrices.Any(x => x.StationId == 1 && x.FuelId == 2 && x.Date == new DateTime(2026, 4, 27)))
            {
                db.FuelPrices.Add(new FuelPrice
                {
                    StationId = 1,
                    FuelId = 2,
                    Date = new DateTime(2026, 4, 27),
                    Price = 55.50m
                });
                db.SaveChanges();
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _keeperConnection?.Dispose();
    }
}
