using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.DAL.Entities;
using LiveFuelMap.DAL.Enums;
using Microsoft.Extensions.Logging;

namespace LiveFuelMap.Infrastructure.Parsing;

public sealed class PriceSourceClientFactory(
    MinfinHtmlPriceSourceClient htmlClient,
    JsonPriceSourceClient jsonClient) : IPriceSourceClientFactory
{
    public IPriceSourceClient Create(DataSource source) =>
        source.Type == DataSourceType.Json ? jsonClient : htmlClient;
}

public sealed class MinfinHtmlPriceSourceClient(
    HttpClient httpClient,
    ILogger<MinfinHtmlPriceSourceClient> logger) : IPriceSourceClient
{
    private static readonly string[] FuelNames = ["А 95+", "А 95", "А 92", "ДП", "Газ"];
    private static readonly HashSet<string> ExcludedStationRows =
    [
        "Бензин А-95 преміум",
        "Бензин А-95",
        "Бензин А-92",
        "Дизельне паливо",
        "Газ автомобільний",
        "Газ авто­мобільний"
    ];

    public async Task<IReadOnlyList<FuelPriceRecord>> FetchAsync(
        DataSource source,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("🚀 Старт парсингу цін пального з Minfin: {Url}", source.Url);

        var html = await httpClient.GetStringAsync(source.Url, cancellationToken);
        var date = ExtractDate(html);
        if (date is null)
        {
            logger.LogWarning("❌ Не знайдено дату у відповіді Minfin.");
            return [];
        }

        logger.LogInformation("📅 Дата: {Date:yyyy-MM-dd}", date.Value);

        var tableHtml = ExtractOperatorTableHtml(html);
        if (string.IsNullOrWhiteSpace(tableHtml))
        {
            logger.LogWarning("❌ Таблиця операторів Minfin не знайдена.");
            return [];
        }

        var rows = Regex.Matches(tableHtml, @"<tr[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (rows.Count == 0)
        {
            logger.LogWarning("❌ Таблиця Minfin не містить рядків.");
            return [];
        }

        var result = new List<FuelPriceRecord>();
        var fuelColumnIndexes = new Dictionary<int, int>();

        foreach (Match rowMatch in rows)
        {
            var cells = ExtractCells(rowMatch.Groups[1].Value);
            if (cells.Count == 0)
                continue;

            if (TryBuildFuelColumnMap(cells, fuelColumnIndexes))
                continue;

            var stationName = cells[0].Trim();
            if (string.IsNullOrWhiteSpace(stationName) ||
                stationName.Equals("Оператор", StringComparison.OrdinalIgnoreCase))
                continue;

            if (ExcludedStationRows.Contains(stationName))
            {
                logger.LogInformation("⛔ Ігноровано службовий рядок \"{Station}\".", stationName);
                continue;
            }

            var prices = ExtractPrices(cells, fuelColumnIndexes);
            if (prices.All(price => price is null or <= 0))
                continue;

            for (var i = 0; i < FuelNames.Length; i++)
            {
                var price = prices[i];
                if (price is null or <= 0)
                    continue;

                result.Add(new FuelPriceRecord(stationName, "Харків", FuelNames[i], price.Value, date.Value));
            }
        }

        logger.LogInformation("✅ Minfin parser завершено. Records={Count}", result.Count);
        return result;
    }

    private static DateTime? ExtractDate(string html)
    {
        var text = CleanCell(html);
        var patterns = new[]
        {
            @"ціни\s+операторів\s+на\s+(\d{1,2})\.(\d{1,2})\.(\d{4})",
            @"середні\s+ціни\s+на\s+пальне\s+на\s+(\d{1,2})\.(\d{1,2})\.(\d{4})",
            @"\bна\s+(\d{1,2})\.(\d{1,2})\.(\d{4})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
                return new DateTime(
                    int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
        }

        return null;
    }

    private static string ExtractOperatorTableHtml(string html)
    {
        var tables = Regex.Matches(html, @"<table[^>]*>.*?</table>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match table in tables)
        {
            var text = CleanCell(table.Value);
            if (text.Contains("Оператор", StringComparison.OrdinalIgnoreCase) &&
                text.Contains("95", StringComparison.OrdinalIgnoreCase) &&
                (text.Contains("ДП", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("ДТ", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("Диз", StringComparison.OrdinalIgnoreCase)))
            {
                return table.Value;
            }
        }

        return string.Empty;
    }

    private static List<string> ExtractCells(string rowHtml)
    {
        var cells = new List<string>();
        var matches = Regex.Matches(rowHtml, @"<t[dh]\b([^>]*)>(.*?)</t[dh]>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var colspan = ExtractColspan(match.Groups[1].Value);
            var value = CleanCell(match.Groups[2].Value);
            for (var i = 0; i < colspan; i++)
                cells.Add(value);
        }

        return cells;
    }

    private static int ExtractColspan(string attributes)
    {
        var match = Regex.Match(attributes, @"\bcolspan\s*=\s*[""']?(\d+)", RegexOptions.IgnoreCase);
        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var colspan))
        {
            return 1;
        }

        return Math.Clamp(colspan, 1, 20);
    }

    private static bool TryBuildFuelColumnMap(IReadOnlyList<string> cells, Dictionary<int, int> fuelColumnIndexes)
    {
        if (!cells.Any(cell => cell.Contains("Оператор", StringComparison.OrdinalIgnoreCase)))
            return false;

        fuelColumnIndexes.Clear();
        for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            var fuelIndex = DetectFuelIndex(cells[cellIndex]);
            if (fuelIndex is not null)
                fuelColumnIndexes[fuelIndex.Value] = cellIndex;
        }

        return fuelColumnIndexes.Count > 0;
    }

    private static List<decimal?> ExtractPrices(IReadOnlyList<string> cells, IReadOnlyDictionary<int, int> fuelColumnIndexes)
    {
        if (fuelColumnIndexes.Count > 0)
        {
            return Enumerable.Range(0, FuelNames.Length)
                .Select(fuelIndex =>
                    fuelColumnIndexes.TryGetValue(fuelIndex, out var cellIndex) && cellIndex < cells.Count
                        ? ParsePrice(cells[cellIndex])
                        : null)
                .ToList();
        }

        var firstLayout = Enumerable.Range(1, FuelNames.Length)
            .Select(index => index < cells.Count ? ParsePrice(cells[index]) : null)
            .ToList();

        if (firstLayout.Any(price => price is > 0))
            return firstLayout;

        return Enumerable.Range(2, FuelNames.Length)
            .Select(index => index < cells.Count ? ParsePrice(cells[index]) : null)
            .ToList();
    }

    private static int? DetectFuelIndex(string value)
    {
        var normalized = value
            .Replace('\u00a0', ' ')
            .Replace('\u00ad', '-')
            .Trim()
            .ToLowerInvariant();

        if (normalized.Contains("95") && normalized.Contains("+")) return 0;
        if (normalized.Contains("95")) return 1;
        if (normalized.Contains("92")) return 2;
        if (normalized.Contains("дп") || normalized.Contains("дт") || normalized.Contains("диз")) return 3;
        if (normalized.Contains("газ")) return 4;
        return null;
    }

    private static decimal? ParsePrice(string value)
    {
        var match = Regex.Match(value.Replace(',', '.'), @"\d+(?:\.\d+)?");
        return match.Success && decimal.TryParse(match.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
            ? price
            : null;
    }

    private static string CleanCell(string html)
    {
        var withoutTags = Regex.Replace(html, "<.*?>", " ");
        return WebUtility.HtmlDecode(withoutTags)
            .Replace('\u00a0', ' ')
            .Trim();
    }
}

public sealed class JsonPriceSourceClient(HttpClient httpClient, ILogger<JsonPriceSourceClient> logger) : IPriceSourceClient
{
    public async Task<IReadOnlyList<FuelPriceRecord>> FetchAsync(DataSource source, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Fetching JSON source {Url}", source.Url);
        var json = await httpClient.GetStringAsync(source.Url, cancellationToken);
        var rows = JsonSerializer.Deserialize<List<JsonFuelPriceRow>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];

        return rows
            .Where(x => !string.IsNullOrWhiteSpace(x.StationName) && !string.IsNullOrWhiteSpace(x.FuelName) && x.Price > 0)
            .Select(x => new FuelPriceRecord(
                x.StationName,
                string.IsNullOrWhiteSpace(x.City) ? "Харків" : x.City,
                x.FuelName,
                x.Price,
                x.Date.Date,
                x.Address,
                x.Latitude,
                x.Longitude))
            .ToList();
    }

    private sealed record JsonFuelPriceRow(
        string StationName,
        string? City,
        string FuelName,
        decimal Price,
        DateTime Date,
        string? Address,
        decimal? Latitude,
        decimal? Longitude);
}
