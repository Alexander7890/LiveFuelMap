using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.DAL.Entities;
using LiveFuelMap.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LiveFuelMap.BLL.Services;

public sealed class ValidatedFuelPriceImportService(
    IUnitOfWork unitOfWork,
    IFuelNormalizer normalizer,
    IPriceChangeDetector priceChangeDetector,
    ILogger<ValidatedFuelPriceImportService> logger) : IFuelPriceImportService
{
    private const string DefaultStationImage = "default-station.jpg";

    private static readonly (int Id, string Code, string Name, int SortOrder)[] FuelCatalog =
    [
        (1, "a95plus", "А 95+", 1),
        (2, "a95", "А 95", 2),
        (3, "a92", "А 92", 3),
        (4, "diesel", "ДП", 4),
        (5, "gas", "Газ", 5)
    ];

    public async Task<FuelImportResult> ImportAsync(
        int sourceId,
        IReadOnlyList<FuelPriceRecord> records,
        CancellationToken cancellationToken = default)
    {
        var saved = 0;
        var changes = new List<PriceChangeNotificationDto>();
        logger.LogInformation("🚀 Старт імпорту цін пального. Records={Count}", records.Count);
        await EnsureFuelCatalogAsync(cancellationToken);

        foreach (var record in records)
        {
            if (!IsValidRecord(record))
            {
                logger.LogWarning("❌ Пропущено невалідний запис parser: {@Record}", record);
                continue;
            }

            var fuelCode = normalizer.NormalizeFuelCode(record.FuelName);
            var fuel = await unitOfWork.Fuels.Query()
                .FirstOrDefaultAsync(x => x.Code == fuelCode, cancellationToken);

            if (fuel is null)
            {
                logger.LogWarning("❌ Невідомий тип пального з parser: {FuelName}; code={FuelCode}", record.FuelName, fuelCode);
                continue;
            }

            var station = await ResolveStationAsync(record, cancellationToken);
            if (station is null)
                continue;

            var date = record.Date.Date;
            var existsToday = await unitOfWork.FuelPrices.Query()
                .FirstOrDefaultAsync(x => x.StationId == station.Id && x.FuelId == fuel.Id && x.Date == date, cancellationToken);

            if (existsToday is not null)
            {
                if (existsToday.Price != record.Price && !existsToday.IsManual)
                {
                    var oldPrice = existsToday.Price;
                    existsToday.Price = record.Price;
                    existsToday.SourceId = sourceId;
                    existsToday.Popularity = 0;
                    saved++;

                    changes.Add(new PriceChangeNotificationDto(
                        station.Id,
                        station.Name,
                        station.City,
                        fuel.Id,
                        fuel.Code,
                        fuel.Name,
                        oldPrice,
                        record.Price,
                        GetChangeType(oldPrice, record.Price),
                        date));

                    logger.LogInformation(
                        "Updated parser price ({Fuel}) on {Date:yyyy-MM-dd}: station={Station}, old={OldPrice}, new={NewPrice}",
                        fuel.Name,
                        date,
                        station.Name,
                        oldPrice,
                        record.Price);
                    continue;
                }

                logger.LogInformation(
                    "🟡 Уже є ({Fuel}) на {Date:yyyy-MM-dd}: station={Station}, price={Price}",
                    fuel.Name,
                    date,
                    station.Name,
                    existsToday.Price);
                continue;
            }

            var latestPrice = await unitOfWork.FuelPrices.Query()
                .Where(x => x.StationId == station.Id && x.FuelId == fuel.Id)
                .Where(x => x.Date < date)
                .OrderByDescending(x => x.Date)
                .ThenByDescending(x => x.Id)
                .Select(x => (decimal?)x.Price)
                .FirstOrDefaultAsync(cancellationToken);

            var isMeaningfulChange = priceChangeDetector.IsMeaningfulChange(latestPrice, record.Price);

            await unitOfWork.FuelPrices.AddAsync(new FuelPrice
            {
                StationId = station.Id,
                FuelId = fuel.Id,
                SourceId = sourceId,
                Date = date,
                Price = record.Price,
                Popularity = 0,
                IsManual = false
            }, cancellationToken);

            saved++;

            if (isMeaningfulChange)
            {
                changes.Add(new PriceChangeNotificationDto(
                    station.Id,
                    station.Name,
                    station.City,
                    fuel.Id,
                    fuel.Code,
                    fuel.Name,
                    latestPrice,
                    record.Price,
                    GetChangeType(latestPrice, record.Price),
                    date));

                logger.LogInformation(
                    "✅ Додано нову ціну ({Fuel}): station={Station}, date={Date:yyyy-MM-dd}, price={Price}",
                    fuel.Name,
                    station.Name,
                    date,
                    record.Price);
            }
            else
            {
                logger.LogInformation(
                    "🟡 Ціна без змін ({Fuel}), але додано точку історії: station={Station}, date={Date:yyyy-MM-dd}, price={Price}",
                    fuel.Name,
                    station.Name,
                    date,
                    record.Price);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("✅ Імпорт цін завершено. Found={Found}; Saved={Saved}", records.Count, saved);
        return new FuelImportResult(records.Count, saved, changes);
    }

    private async Task EnsureFuelCatalogAsync(CancellationToken cancellationToken)
    {
        var fuels = await unitOfWork.Fuels.Query().ToListAsync(cancellationToken);

        foreach (var item in FuelCatalog)
        {
            var fuel = fuels.FirstOrDefault(x => x.Id == item.Id) ?? fuels.FirstOrDefault(x => x.Code == item.Code);
            if (fuel is null)
            {
                await unitOfWork.Fuels.AddAsync(new Fuel
                {
                    Id = item.Id,
                    Code = item.Code,
                    Name = item.Name,
                    SortOrder = item.SortOrder
                }, cancellationToken);
                continue;
            }

            fuel.Code = item.Code;
            fuel.Name = item.Name;
            fuel.SortOrder = item.SortOrder;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("🧾 Типи пального перевірено.");
    }

    private async Task<Station?> ResolveStationAsync(FuelPriceRecord record, CancellationToken cancellationToken)
    {
        var stationKey = normalizer.NormalizeStationKey(record.StationName);
        var city = string.IsNullOrWhiteSpace(record.City) ? "Харків" : record.City.Trim();

        var station = await unitOfWork.Stations.Query()
            .FirstOrDefaultAsync(x => x.NormalizedKey == stationKey && x.City == city, cancellationToken);

        station ??= await unitOfWork.Stations.Query()
            .FirstOrDefaultAsync(x => x.NormalizedKey == stationKey && x.IsActive, cancellationToken);

        if (station is not null)
        {
            if (!station.IsActive)
                station.IsActive = true;

            if (!string.Equals(station.City, city, StringComparison.Ordinal))
                station.City = city;

            ApplyStationDetailsFromRecord(station, record);
            return station;
        }

        station = new Station
        {
            Name = record.StationName.Trim(),
            NormalizedKey = stationKey,
            City = city,
            Address = BuildStationAddress(record, city),
            Latitude = record.Latitude ?? 0,
            Longitude = record.Longitude ?? 0,
            ImageUrl = DefaultStationImage,
            IsActive = true
        };

        await unitOfWork.Stations.AddAsync(station, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "➕ Додано нову АЗС з parser: {Station}. Address={Address}; Latitude={Latitude}; Longitude={Longitude}",
            station.Name,
            station.Address,
            station.Latitude,
            station.Longitude);
        return station;
    }

    private static void ApplyStationDetailsFromRecord(Station station, FuelPriceRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.Address) &&
            !string.Equals(station.Address, record.Address.Trim(), StringComparison.Ordinal))
        {
            station.Address = record.Address.Trim();
        }

        if (record.Latitude is not null && record.Longitude is not null)
        {
            station.Latitude = record.Latitude.Value;
            station.Longitude = record.Longitude.Value;
        }

        if (string.IsNullOrWhiteSpace(station.ImageUrl))
            station.ImageUrl = DefaultStationImage;
    }

    private static string BuildStationAddress(FuelPriceRecord record, string city)
    {
        if (!string.IsNullOrWhiteSpace(record.Address))
            return record.Address.Trim();

        return string.IsNullOrWhiteSpace(city)
            ? "Minfin operator price row"
            : $"{city}: Minfin operator price row";
    }

    private static bool IsValidRecord(FuelPriceRecord record) =>
        !string.IsNullOrWhiteSpace(record.StationName) &&
        !string.IsNullOrWhiteSpace(record.FuelName) &&
        record.Price > 0 &&
        record.Date != default;

    private static string GetChangeType(decimal? oldPrice, decimal newPrice)
    {
        if (oldPrice is null) return "new";
        return newPrice > oldPrice.Value ? "increase" : "decrease";
    }
}
