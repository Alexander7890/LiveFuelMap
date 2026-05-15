using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Services;
using LiveFuelMap.DAL.Entities;
using LiveFuelMap.DAL.Persistence;
using LiveFuelMap.DAL.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiveFuelMap.Tests.Unit;

public sealed class FuelPriceHistoryAndImportTests
{
    [Fact]
    public async Task ImportAsync_AddsNewDatedRecord_WhenPriceDidNotChange()
    {
        await using var context = await CreateContextAsync();
        var station = await context.Stations.FirstAsync(x => x.Id == 1);
        var fuel = await context.Fuels.FirstAsync(x => x.Id == 2);

        context.FuelPrices.Add(new FuelPrice
        {
            StationId = station.Id,
            FuelId = fuel.Id,
            Date = new DateTime(2026, 4, 30),
            Price = 70.99m
        });
        await context.SaveChangesAsync();

        var service = CreateImportService(context);
        var result = await service.ImportAsync(1, [
            new FuelPriceRecord(station.Name, station.City, fuel.Name, 70.99m, new DateTime(2026, 5, 1))
        ]);

        Assert.Equal(1, result.RecordsSaved);
        Assert.Empty(result.PriceChanges ?? []);
        Assert.True(await context.FuelPrices.AnyAsync(x =>
            x.StationId == station.Id &&
            x.FuelId == fuel.Id &&
            x.Date == new DateTime(2026, 5, 1) &&
            x.Price == 70.99m));
    }

    [Fact]
    public async Task ImportAsync_UpdatesExistingParserRecordForSameDate_WhenSourcePriceChanged()
    {
        await using var context = await CreateContextAsync();
        var station = await context.Stations.FirstAsync(x => x.Id == 1);
        var fuel = await context.Fuels.FirstAsync(x => x.Id == 2);
        var date = new DateTime(2026, 5, 1);

        context.FuelPrices.Add(new FuelPrice
        {
            StationId = station.Id,
            FuelId = fuel.Id,
            Date = date,
            Price = 70.99m
        });
        await context.SaveChangesAsync();

        var service = CreateImportService(context);
        var result = await service.ImportAsync(1, [
            new FuelPriceRecord(station.Name, station.City, fuel.Name, 71.99m, date)
        ]);

        var price = await context.FuelPrices.SingleAsync(x =>
            x.StationId == station.Id &&
            x.FuelId == fuel.Id &&
            x.Date == date);

        Assert.Equal(1, result.RecordsSaved);
        Assert.Equal(71.99m, price.Price);
        Assert.NotEmpty(result.PriceChanges ?? []);
    }

    [Fact]
    public async Task ImportAsync_DoesNotOverwriteManualRecordForSameDate()
    {
        await using var context = await CreateContextAsync();
        var station = await context.Stations.FirstAsync(x => x.Id == 1);
        var fuel = await context.Fuels.FirstAsync(x => x.Id == 2);
        var date = new DateTime(2026, 5, 1);

        context.FuelPrices.Add(new FuelPrice
        {
            StationId = station.Id,
            FuelId = fuel.Id,
            Date = date,
            Price = 70.99m,
            IsManual = true
        });
        await context.SaveChangesAsync();

        var service = CreateImportService(context);
        var result = await service.ImportAsync(1, [
            new FuelPriceRecord(station.Name, station.City, fuel.Name, 71.99m, date)
        ]);

        var price = await context.FuelPrices.SingleAsync(x =>
            x.StationId == station.Id &&
            x.FuelId == fuel.Id &&
            x.Date == date);

        Assert.Equal(0, result.RecordsSaved);
        Assert.Equal(70.99m, price.Price);
    }

    [Fact]
    public async Task ImportAsync_CreatesUnknownParserStation_WhenRecordHasNoLocation()
    {
        await using var context = await CreateContextAsync();
        var a95 = await context.Fuels.SingleAsync(x => x.Code == "a95");
        var diesel = await context.Fuels.SingleAsync(x => x.Code == "diesel");
        var date = new DateTime(2026, 5, 11);

        var service = CreateImportService(context);
        var result = await service.ImportAsync(1, [
            new FuelPriceRecord("Brent Oil", "Харків", a95.Name, 66.85m, date),
            new FuelPriceRecord("Brent Oil", "Харків", diesel.Name, 86.75m, date)
        ]);

        var station = await context.Stations.SingleAsync(x => x.NormalizedKey == "brent-oil");
        var prices = await context.FuelPrices
            .Where(x => x.StationId == station.Id && x.Date == date)
            .OrderBy(x => x.FuelId)
            .ToListAsync();

        Assert.Equal(2, result.RecordsSaved);
        Assert.Equal("Brent Oil", station.Name);
        Assert.Equal("Харків", station.City);
        Assert.Equal("Харків: Minfin operator price row", station.Address);
        Assert.Equal(0, station.Latitude);
        Assert.Equal(0, station.Longitude);
        Assert.Equal("default-station.jpg", station.ImageUrl);
        Assert.Equal(2, prices.Count);
        Assert.Contains(prices, x => x.FuelId == a95.Id && x.Price == 66.85m);
        Assert.Contains(prices, x => x.FuelId == diesel.Id && x.Price == 86.75m);
    }

    [Fact]
    public async Task GetPriceHistoryAsync_UsesLatestAvailableDate_WhenNoRangeIsSpecified()
    {
        await using var context = await CreateContextAsync();
        var station = await context.Stations.FirstAsync(x => x.Id == 1);
        var fuel = await context.Fuels.FirstAsync(x => x.Id == 2);

        context.FuelPrices.Add(new FuelPrice
        {
            StationId = station.Id,
            FuelId = fuel.Id,
            Date = new DateTime(2026, 3, 10),
            Price = 67.99m
        });
        await context.SaveChangesAsync();

        var service = new FuelDataService(new UnitOfWork(context));
        var history = await service.GetPriceHistoryAsync(new PriceHistoryQuery(fuel.Code));

        Assert.Contains("2026-03-10", history.Labels);
        Assert.NotEmpty(history.Datasets);
        Assert.Contains(history.Datasets, x => x.Data.Contains(67.99m));
    }

    [Fact]
    public async Task GetPriceHistoryAsync_CarriesLastKnownStationPrice_WhenAnotherStationChanges()
    {
        await using var context = await CreateContextAsync();
        var fuel = await context.Fuels.FirstAsync(x => x.Id == 2);

        context.FuelPrices.AddRange(
            new FuelPrice
            {
                StationId = 1,
                FuelId = fuel.Id,
                Date = new DateTime(2026, 4, 30),
                Price = 70.00m
            },
            new FuelPrice
            {
                StationId = 2,
                FuelId = fuel.Id,
                Date = new DateTime(2026, 4, 30),
                Price = 71.00m
            },
            new FuelPrice
            {
                StationId = 2,
                FuelId = fuel.Id,
                Date = new DateTime(2026, 5, 1),
                Price = 72.00m
            });
        await context.SaveChangesAsync();

        var service = new FuelDataService(new UnitOfWork(context));
        var history = await service.GetPriceHistoryAsync(new PriceHistoryQuery(fuel.Code));
        var amic = history.Datasets.Single(x => x.Label == "AMIC");

        Assert.Equal(["2026-04-30", "2026-05-01"], history.Labels);
        Assert.Equal([70.00m, 70.00m], amic.Data);
    }

    private static ValidatedFuelPriceImportService CreateImportService(LiveFuelMapDbContext context) =>
        new(
            new UnitOfWork(context),
            new UnicodeFuelNormalizer(),
            new PriceChangeDetector(),
            NullLogger<ValidatedFuelPriceImportService>.Instance);

    private static async Task<LiveFuelMapDbContext> CreateContextAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LiveFuelMapDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new LiveFuelMapDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
