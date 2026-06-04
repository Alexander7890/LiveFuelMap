using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.DAL.Entities;
using LiveFuelMap.DAL.Enums;
using LiveFuelMap.Infrastructure.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LiveFuelMap.Infrastructure.AI;

public sealed class ExternalAutomotiveContextService(
    HttpClient httpClient,
    MinfinHtmlPriceSourceClient minfinClient,
    IOptions<ExternalContextOptions> options,
    ILogger<ExternalAutomotiveContextService> logger) : IExternalAutomotiveContextService
{
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    private static readonly Dictionary<string, string> CityAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["харків"] = "Харків",
        ["харкова"] = "Харків",
        ["харьков"] = "Харків",
        ["харькова"] = "Харків",
        ["львів"] = "Львів",
        ["львова"] = "Львів",
        ["львов"] = "Львів",
        ["київ"] = "Київ",
        ["києва"] = "Київ",
        ["киев"] = "Київ",
        ["киева"] = "Київ",
        ["одеса"] = "Одеса",
        ["одеси"] = "Одеса",
        ["одесса"] = "Одеса",
        ["одессы"] = "Одеса",
        ["дніпро"] = "Дніпро",
        ["дніпра"] = "Дніпро",
        ["днепр"] = "Дніпро",
        ["днепра"] = "Дніпро",
        ["полтава"] = "Полтава",
        ["полтави"] = "Полтава",
        ["суми"] = "Суми",
        ["сум"] = "Суми",
        ["чернігів"] = "Чернігів",
        ["чернігова"] = "Чернігів",
        ["чернигов"] = "Чернігів",
        ["чернигова"] = "Чернігів"
    };

    private static readonly Dictionary<string, string> UtfCityAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["харків"] = "Харків",
        ["харкова"] = "Харків",
        ["харьков"] = "Харків",
        ["харькова"] = "Харків",
        ["львів"] = "Львів",
        ["львова"] = "Львів",
        ["львов"] = "Львів",
        ["київ"] = "Київ",
        ["києва"] = "Київ",
        ["киев"] = "Київ",
        ["киева"] = "Київ",
        ["одеса"] = "Одеса",
        ["одеси"] = "Одеса",
        ["одесса"] = "Одеса",
        ["одессы"] = "Одеса",
        ["дніпро"] = "Дніпро",
        ["дніпра"] = "Дніпро",
        ["днепр"] = "Дніпро",
        ["днепра"] = "Дніпро",
        ["полтава"] = "Полтава",
        ["полтави"] = "Полтава",
        ["суми"] = "Суми",
        ["сум"] = "Суми",
        ["чернігів"] = "Чернігів",
        ["чернігова"] = "Чернігів",
        ["чернигов"] = "Чернігів",
        ["чернигова"] = "Чернігів"
    };

    private readonly ExternalContextOptions _options = options.Value;

    public async Task<string> BuildContextAsync(ChatRequest request, ChatTopicDecision topic, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return string.Empty;

        try
        {
            httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 3, 60));

            return topic.Intent switch
            {
                "route-distance" => await BuildRouteContextAsync(request.Message, cancellationToken),
                "car-buying-advice" => await BuildCarBuyingContextAsync(request.Message, cancellationToken),
                "car-advice" => await BuildSearchContextAsync(request.Message, cancellationToken),
                _ => await BuildFuelInternetContextAsync(request, topic.Intent, cancellationToken)
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "External automotive context lookup failed for intent {Intent}.", topic.Intent);
            return """
                Перевірка через відкриті інтернет-джерела:
                - Зовнішню перевірку зараз не вдалося виконати.
                - Не називай зовнішні дані як перевірений факт; дай загальну пораду і вкажи, що інформацію потрібно перевірити вручну.
                """;
        }
    }

    public async Task<string?> BuildDirectAnswerAsync(ChatRequest request, ChatTopicDecision topic, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || topic.Intent != "route-distance")
            return null;

        try
        {
            httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 3, 60));
            return await BuildRouteDirectAnswerAsync(request.Message, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "External route direct answer lookup failed.");
            return "Не вдалося зараз перевірити маршрут через зовнішні джерела. Це не дані з бази LiveFuelMap; перевірте відстань у картах або спробуйте ще раз пізніше.";
        }
    }

    private async Task<string?> BuildRouteDirectAnswerAsync(string message, CancellationToken cancellationToken)
    {
        if (!TryExtractRoute(message, out var from, out var to))
            return "Не вдалося однозначно визначити маршрут. Напишіть, наприклад: \"від Харкова до Львова\". Це не дані з бази LiveFuelMap; відстань потрібно перевіряти за актуальними картами.";

        var fromPoint = await GeocodeAsync(from, cancellationToken);
        var toPoint = await GeocodeAsync(to, cancellationToken);
        if (fromPoint is null || toPoint is null)
            return $"Не вдалося знайти координати для маршруту {from} -> {to}. Це не дані з бази LiveFuelMap; уточніть назви населених пунктів і перевірте маршрут у картах.";

        var route = await FetchOsrmRouteAsync(fromPoint.Value, toPoint.Value, cancellationToken);
        if (route is null)
            return $"OSRM зараз не повернув автомобільний маршрут {from} -> {to}. Це не дані з бази LiveFuelMap; перевірте актуальну відстань у Google Maps, OpenStreetMap або іншій навігації.";

        var checkedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", InvariantCulture);
        var distance = route.Value.DistanceKm.ToString("0.#", InvariantCulture);
        var duration = FormatDurationForAnswer(route.Value.DurationSeconds);

        return $"Орієнтовна автомобільна відстань від {from} до {to} — {distance} км. Орієнтовний час у дорозі без заторів і зупинок — {duration}. Джерела: OpenStreetMap Nominatim та OSRM public route API, перевірено {checkedAt}. Це не дані з бази LiveFuelMap; фактичний маршрут, час і доступність доріг потрібно перевіряти в актуальній навігації.";
    }

    private async Task<string> BuildRouteContextAsync(string message, CancellationToken cancellationToken)
    {
        if (!TryExtractRoute(message, out var from, out var to))
        {
            return """
                Перевірка через відкриті інтернет-джерела:
                - Маршрут у повідомленні не вдалося однозначно розпізнати.
                - Попроси користувача уточнити формат, наприклад: "від Харкова до Львова".
                """;
        }

        var fromPoint = await GeocodeAsync(from, cancellationToken);
        var toPoint = await GeocodeAsync(to, cancellationToken);
        if (fromPoint is null || toPoint is null)
        {
            return $"""
                Перевірка через відкриті інтернет-джерела:
                - Джерело: OpenStreetMap Nominatim.
                - Не вдалося знайти координати для маршруту {from} -> {to}.
                - Не вигадуй відстань; попроси уточнити населені пункти.
                """;
        }

        var route = await FetchOsrmRouteAsync(fromPoint.Value, toPoint.Value, cancellationToken);
        if (route is null)
        {
            return $"""
                Перевірка через відкриті інтернет-джерела:
                - Джерело координат: OpenStreetMap Nominatim.
                - Маршрут: {from} -> {to}.
                - OSRM зараз не повернув автомобільний маршрут.
                - Не вигадуй точну відстань; поясни, що потрібна додаткова перевірка в картах.
                """;
        }

        var checkedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", InvariantCulture);
        return $"""
            Перевірка через відкриті інтернет-джерела:
            - Джерела: OpenStreetMap Nominatim для координат; OSRM public route API для автомобільного маршруту.
            - Дата перевірки: {checkedAt}.
            - Маршрут: {from} -> {to}.
            - Орієнтовна автомобільна відстань: {route.Value.DistanceKm.ToString("0.#", InvariantCulture)} км.
            - Орієнтовний час у дорозі без зупинок, заторів і прикордонних/дорожніх затримок: {FormatDuration(route.Value.DurationSeconds)}.
            - Це зовнішня перевірка, а не дані з бази LiveFuelMap; фактичний маршрут може відрізнятися залежно від доріг і навігації.
            """;
    }

    private async Task<string> BuildCarBuyingContextAsync(string message, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Перевірка через відкриті інтернет-джерела:");
        builder.AppendLine("- Запит стосується вибору авто; це не дані з бази LiveFuelMap.");

        var budget = TryExtractBudgetUah(message);
        if (budget is not null)
        {
            var usdRate = await FetchUsdRateAsync(cancellationToken);
            if (usdRate is not null && usdRate.Value.Rate > 0)
            {
                var budgetUsd = budget.Value / usdRate.Value.Rate;
                builder.AppendLine($"- Бюджет із запиту: {budget.Value.ToString("0", InvariantCulture)} грн.");
                builder.AppendLine($"- Довідково за курсом НБУ USD {usdRate.Value.Rate.ToString("0.####", InvariantCulture)} грн від {usdRate.Value.Date}: приблизно {budgetUsd.ToString("0", InvariantCulture)} USD.");
            }
            else
            {
                builder.AppendLine($"- Бюджет із запиту: {budget.Value.ToString("0", InvariantCulture)} грн. Курс НБУ зараз не вдалося отримати.");
            }
        }

        var snippets = await FetchDuckDuckGoSnippetsAsync(BuildCarSearchQuery(message), cancellationToken);
        AppendSearchSnippets(builder, snippets);
        builder.AppendLine("- У відповіді не стверджуй наявність конкретного авто в продажу; радь перевірити актуальні оголошення, VIN, історію ДТП, пробіг, сервіс і діагностику.");
        return builder.ToString();
    }

    private async Task<string> BuildFuelInternetContextAsync(ChatRequest request, string intent, CancellationToken cancellationToken)
    {
        var source = new DataSource
        {
            Id = 0,
            Name = "Minfin Kharkiv external fallback",
            Url = string.IsNullOrWhiteSpace(_options.MinfinFuelEndpoint)
                ? "https://index.minfin.com.ua/ua/markets/fuel/reg/harkovskaya/"
                : _options.MinfinFuelEndpoint,
            Type = DataSourceType.Html,
            Enabled = true
        };

        var records = await minfinClient.FetchAsync(source, cancellationToken);
        if (records.Count == 0)
            return await BuildSearchContextAsync($"{request.Message} ціни пальне Харків АЗС", cancellationToken);

        var filtered = FilterFuelRecords(records, request);
        filtered = intent switch
        {
            "cheapest-price" => filtered.OrderBy(x => x.Price).ThenBy(x => x.StationName).Take(20).ToList(),
            "highest-price" => filtered.OrderByDescending(x => x.Price).ThenBy(x => x.StationName).Take(20).ToList(),
            _ => filtered.OrderBy(x => x.StationName).ThenBy(x => FuelSortOrder(x.FuelName)).Take(40).ToList()
        };

        if (filtered.Count == 0)
            return await BuildSearchContextAsync($"{request.Message} ціни пальне Харків АЗС", cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine("Перевірка через відкриті інтернет-джерела:");
        builder.AppendLine("- У базі LiveFuelMap не знайдено актуальних даних за цим запитом, тому backend перевірив зовнішнє джерело.");
        builder.AppendLine($"- Джерело: Minfin, {source.Url}");
        builder.AppendLine($"- Дата перевірки: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", InvariantCulture)}.");
        builder.AppendLine($"- Дата цін у джерелі: {filtered[0].Date:yyyy-MM-dd}.");
        builder.AppendLine("- Дані нижче не записуються автоматично у відповідь як дані сайту LiveFuelMap; це fallback з відкритого джерела.");

        foreach (var record in filtered)
        {
            builder.Append("- Оператор: ").Append(record.StationName)
                .Append("; місто/регіон: ").Append(record.City)
                .Append("; тип пального: ").Append(record.FuelName)
                .Append("; ціна: ").Append(record.Price.ToString("0.00", InvariantCulture))
                .Append(" грн/л; дата: ").Append(record.Date.ToString("yyyy-MM-dd", InvariantCulture))
                .AppendLine(".");
        }

        if (intent is "price-change")
            builder.AppendLine("- Minfin fallback повертає поточну таблицю, але не дає попередню ціну з твоєї БД; не вигадуй відсоток зміни, якщо його немає в контексті.");

        return builder.ToString();
    }

    private async Task<string> BuildSearchContextAsync(string message, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Перевірка через відкриті інтернет-джерела:");
        builder.AppendLine("- Запит стосується загальної автомобільної тематики; це не дані з бази LiveFuelMap.");

        var snippets = await FetchDuckDuckGoSnippetsAsync($"{message} авто Україна", cancellationToken);
        AppendSearchSnippets(builder, snippets);
        builder.AppendLine("- Якщо фрагменти не дають достатньої фактичної бази, дай загальну пораду без точних неперевірених чисел.");
        return builder.ToString();
    }

    private async Task<GeoPoint?> GeocodeAsync(string city, CancellationToken cancellationToken)
    {
        var query = $"{city}, Україна";
        var url = AddQueryString(_options.NominatimEndpoint,
            ("format", "jsonv2"),
            ("limit", "1"),
            ("countrycodes", "ua"),
            ("q", query));

        using var request = BuildGetRequest(url);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Nominatim returned {StatusCode} for {City}.", (int)response.StatusCode, city);
            return null;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            return null;

        var first = document.RootElement[0];
        if (!first.TryGetProperty("lat", out var latElement) ||
            !first.TryGetProperty("lon", out var lonElement) ||
            !decimal.TryParse(latElement.GetString(), NumberStyles.Float, InvariantCulture, out var lat) ||
            !decimal.TryParse(lonElement.GetString(), NumberStyles.Float, InvariantCulture, out var lon))
        {
            return null;
        }

        var name = first.TryGetProperty("display_name", out var displayName)
            ? displayName.GetString()
            : city;

        return new GeoPoint((double)lat, (double)lon, name ?? city);
    }

    private async Task<RouteInfo?> FetchOsrmRouteAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken)
    {
        var baseEndpoint = _options.OsrmEndpoint.TrimEnd('/');
        var url = $"{baseEndpoint}/route/v1/driving/{from.Lon.ToString(InvariantCulture)},{from.Lat.ToString(InvariantCulture)};{to.Lon.ToString(InvariantCulture)},{to.Lat.ToString(InvariantCulture)}?overview=false&alternatives=false&steps=false";

        using var request = BuildGetRequest(url);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("OSRM returned {StatusCode} for route {From} -> {To}.", (int)response.StatusCode, from.DisplayName, to.DisplayName);
            return null;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("routes", out var routes) ||
            routes.ValueKind != JsonValueKind.Array ||
            routes.GetArrayLength() == 0)
        {
            return null;
        }

        var first = routes[0];
        if (!first.TryGetProperty("distance", out var distanceElement) ||
            !first.TryGetProperty("duration", out var durationElement))
        {
            return null;
        }

        var distanceMeters = distanceElement.GetDouble();
        var durationSeconds = durationElement.GetDouble();
        return new RouteInfo(distanceMeters / 1000d, durationSeconds);
    }

    private async Task<UsdRate?> FetchUsdRateAsync(CancellationToken cancellationToken)
    {
        var endpoint = _options.NbuExchangeEndpoint.TrimEnd('?', '&');
        var separator = endpoint.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var url = $"{endpoint}{separator}valcode=USD&json";
        using var request = BuildGetRequest(url);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            return null;

        var item = document.RootElement[0];
        if (!item.TryGetProperty("rate", out var rateElement))
            return null;

        var rate = rateElement.GetDecimal();
        var date = item.TryGetProperty("exchangedate", out var dateElement)
            ? dateElement.GetString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd", InvariantCulture)
            : DateTime.UtcNow.ToString("yyyy-MM-dd", InvariantCulture);

        return new UsdRate(rate, date);
    }

    private async Task<IReadOnlyList<SearchSnippet>> FetchDuckDuckGoSnippetsAsync(string query, CancellationToken cancellationToken)
    {
        var url = AddQueryString(_options.DuckDuckGoEndpoint,
            ("q", query),
            ("format", "json"),
            ("no_redirect", "1"),
            ("no_html", "1"),
            ("kl", "uk-ua"));

        using var request = BuildGetRequest(url);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("DuckDuckGo returned {StatusCode} for query {Query}.", (int)response.StatusCode, query);
            return [];
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var snippets = new List<SearchSnippet>();

        if (document.RootElement.TryGetProperty("AbstractText", out var abstractText) &&
            !string.IsNullOrWhiteSpace(abstractText.GetString()))
        {
            var urlText = document.RootElement.TryGetProperty("AbstractURL", out var abstractUrl)
                ? abstractUrl.GetString()
                : null;
            snippets.Add(new SearchSnippet("DuckDuckGo Abstract", abstractText.GetString()!, urlText));
        }

        if (document.RootElement.TryGetProperty("RelatedTopics", out var relatedTopics))
            CollectRelatedTopics(relatedTopics, snippets);

        if (snippets.Count == 0)
            snippets.AddRange(await FetchDuckDuckGoHtmlResultsAsync(query, cancellationToken));

        return snippets
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .DistinctBy(x => x.Text)
            .Take(5)
            .ToList();
    }

    private async Task<IReadOnlyList<SearchSnippet>> FetchDuckDuckGoHtmlResultsAsync(string query, CancellationToken cancellationToken)
    {
        var url = AddQueryString(_options.DuckDuckGoHtmlEndpoint, ("q", query));
        using var request = BuildGetRequest(url);
        request.Headers.Remove("Accept");
        request.Headers.TryAddWithoutValidation("Accept", "text/html,*/*");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("DuckDuckGo HTML search returned {StatusCode} for query {Query}.", (int)response.StatusCode, query);
            return [];
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var matches = Regex.Matches(
            html,
            "(?s)<a[^>]*class=\"result__a\"[^>]*href=\"(?<url>[^\"]+)\"[^>]*>(?<title>.*?)</a>.*?<a[^>]*class=\"result__snippet\"[^>]*>(?<snippet>.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var snippets = new List<SearchSnippet>();
        foreach (Match match in matches.Take(5))
        {
            var title = CleanHtml(match.Groups["title"].Value);
            var snippet = CleanHtml(match.Groups["snippet"].Value);
            var resultUrl = NormalizeDuckDuckGoUrl(match.Groups["url"].Value);

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(snippet))
                continue;

            snippets.Add(new SearchSnippet(
                "DuckDuckGo HTML Search",
                string.IsNullOrWhiteSpace(snippet) ? title : $"{title}: {snippet}",
                resultUrl));
        }

        return snippets;
    }

    private static void CollectRelatedTopics(JsonElement element, List<SearchSnippet> snippets)
    {
        if (snippets.Count >= 5 || element.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in element.EnumerateArray())
        {
            if (snippets.Count >= 5)
                return;

            if (item.TryGetProperty("Text", out var textElement))
            {
                var text = textElement.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var firstUrl = item.TryGetProperty("FirstURL", out var urlElement)
                        ? urlElement.GetString()
                        : null;
                    snippets.Add(new SearchSnippet("DuckDuckGo RelatedTopic", text, firstUrl));
                }
            }

            if (item.TryGetProperty("Topics", out var nested))
                CollectRelatedTopics(nested, snippets);
        }
    }

    private static void AppendSearchSnippets(StringBuilder builder, IReadOnlyList<SearchSnippet> snippets)
    {
        builder.AppendLine($"- Дата перевірки: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", InvariantCulture)}.");
        builder.AppendLine("- Джерело пошукового контексту: DuckDuckGo Instant Answer API або DuckDuckGo HTML Search fallback.");

        if (snippets.Count == 0)
        {
            builder.AppendLine("- DuckDuckGo не повернув коротких релевантних фрагментів для цього запиту; відповідь має бути загальною без тверджень про актуальні оголошення або точні ціни.");
            return;
        }

        foreach (var snippet in snippets)
        {
            var trimmedText = snippet.Text.Length > 280 ? snippet.Text[..280] + "..." : snippet.Text;
            builder.Append("- ").Append(trimmedText);
            if (!string.IsNullOrWhiteSpace(snippet.Url))
                builder.Append(" (").Append(snippet.Url).Append(')');
            builder.AppendLine();
        }
    }

    private static bool TryExtractRoute(string message, out string from, out string to)
    {
        from = string.Empty;
        to = string.Empty;

        var normalized = Regex.Replace(message.Trim(), @"\s+", " ");
        var utfMatch = Regex.Match(
            normalized,
            @"(?:від|от|from|із|из|з)\s+(?<from>[\p{L}'’\-\s]+?)\s+(?:до|to|в|у)\s+(?<to>[\p{L}'’\-\s]+?)(?:[?.!,]|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (utfMatch.Success)
        {
            from = NormalizeCityName(utfMatch.Groups["from"].Value);
            to = NormalizeCityName(utfMatch.Groups["to"].Value);
            return !string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to);
        }

        var match = Regex.Match(
            normalized,
            @"(?:від|из|з)\s+(?<from>[\p{L}'’\-\s]+?)\s+(?:до|в|у)\s+(?<to>[\p{L}'’\-\s]+?)(?:[?.!,]|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (match.Success)
        {
            from = NormalizeCityName(match.Groups["from"].Value);
            to = NormalizeCityName(match.Groups["to"].Value);
            return !string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to);
        }

        var utfFound = UtfCityAliases
            .Where(alias => Regex.IsMatch(normalized, $@"(^|\s){Regex.Escape(alias.Key)}(\s|[?.!,]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .Select(alias => alias.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        if (utfFound.Count >= 2)
        {
            from = utfFound[0];
            to = utfFound[1];
            return true;
        }

        var found = CityAliases
            .Where(alias => Regex.IsMatch(normalized, $@"(^|\s){Regex.Escape(alias.Key)}(\s|[?.!,]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .Select(alias => alias.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        if (found.Count < 2)
            return false;

        from = found[0];
        to = found[1];
        return true;
    }

    private static string NormalizeCityName(string value)
    {
        var compact = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^\p{L}\s'-]", string.Empty);
        compact = Regex.Replace(compact, @"\s+", " ");
        if (UtfCityAliases.TryGetValue(compact, out var utfCanonical))
            return utfCanonical;

        return CityAliases.TryGetValue(compact, out var canonical)
            ? canonical
            : CultureInfo.GetCultureInfo("uk-UA").TextInfo.ToTitleCase(compact);
    }

    private static decimal? TryExtractBudgetUah(string message)
    {
        var match = Regex.Match(
            message.ToLowerInvariant(),
            @"(?<amount>\d+(?:[.,]\d+)?)\s*(?<unit>к|k|тис\.?|тисяч)?\s*(грн|uah|₴)?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!match.Success ||
            !decimal.TryParse(match.Groups["amount"].Value.Replace(',', '.'), NumberStyles.Number, InvariantCulture, out var amount))
        {
            return null;
        }

        var unit = match.Groups["unit"].Value;
        if (!string.IsNullOrWhiteSpace(unit) || amount < 5000)
            amount *= 1000;

        return amount >= 10000 ? amount : null;
    }

    private static string BuildCarSearchQuery(string message)
    {
        var budget = TryExtractBudgetUah(message);
        return budget is null
            ? $"{message} авто Україна огляд надійність"
            : $"яке авто купити до {budget.Value.ToString("0", InvariantCulture)} грн Україна надійні моделі";
    }

    private static IReadOnlyList<FuelPriceRecord> FilterFuelRecords(IReadOnlyList<FuelPriceRecord> records, ChatRequest request)
    {
        var message = NormalizeSearchText(request.Message);
        var fuelNames = DetectFuelNames(message, request.FuelCode);
        var stationName = DetectStationName(records.Select(x => x.StationName).Distinct(), message);

        var filtered = records.AsEnumerable();
        if (fuelNames.Count > 0)
            filtered = filtered.Where(x => fuelNames.Contains(x.FuelName, StringComparer.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(stationName))
            filtered = filtered.Where(x => NormalizeSearchText(x.StationName).Contains(stationName));

        return filtered.ToList();
    }

    private static List<string> DetectFuelNames(string normalizedMessage, string? requestedFuelCode)
    {
        var result = new List<string>();
        var requested = requestedFuelCode?.Trim().ToLowerInvariant();

        if (requested == "a95plus" || normalizedMessage.Contains("95+") || normalizedMessage.Contains("a95plus"))
            result.Add("А 95+");
        if (requested == "a95" || Regex.IsMatch(normalizedMessage, @"(^|[^0-9])95([^0-9+]|$)") || normalizedMessage.Contains("a95"))
            result.Add("А 95");
        if (requested == "a92" || normalizedMessage.Contains("92") || normalizedMessage.Contains("a92"))
            result.Add("А 92");
        if (requested == "diesel" || normalizedMessage.Contains("диз") || normalizedMessage.Contains("дт") || normalizedMessage.Contains("diesel"))
            result.Add("ДП");
        if (requested == "gas" || normalizedMessage.Contains("газ") || normalizedMessage.Contains("lpg"))
            result.Add("Газ");

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? DetectStationName(IEnumerable<string> stationNames, string normalizedMessage)
    {
        foreach (var station in stationNames)
        {
            var normalizedStation = NormalizeSearchText(station);
            if (normalizedStation.Length >= 3 && normalizedMessage.Contains(normalizedStation))
                return normalizedStation;
        }

        return null;
    }

    private static int FuelSortOrder(string fuelName) =>
        fuelName switch
        {
            "А 95+" => 1,
            "А 95" => 2,
            "А 92" => 3,
            "ДП" => 4,
            "Газ" => 5,
            _ => 99
        };

    private static string NormalizeSearchText(string value) =>
        Regex.Replace(value.Trim().ToLowerInvariant(), @"[\s_\-\.]+", string.Empty);

    private static string CleanHtml(string value)
    {
        var withoutTags = Regex.Replace(value, "<[^>]+>", " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    private static string? NormalizeDuckDuckGoUrl(string value)
    {
        var decoded = WebUtility.HtmlDecode(value);
        if (decoded.StartsWith("//", StringComparison.Ordinal))
            decoded = "https:" + decoded;

        var match = Regex.Match(decoded, @"[?&]uddg=(?<url>[^&]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (match.Success)
            return Uri.UnescapeDataString(match.Groups["url"].Value);

        return decoded.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? decoded
            : null;
    }

    private static string FormatDuration(double seconds)
    {
        var duration = TimeSpan.FromSeconds(seconds);
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours} год {duration.Minutes} хв";

        return $"{duration.Minutes} хв";
    }

    private static string FormatDurationForAnswer(double seconds)
    {
        var duration = TimeSpan.FromSeconds(seconds);
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours} год {duration.Minutes} хв"
            : $"{duration.Minutes} хв";
    }

    private HttpRequestMessage BuildGetRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", string.IsNullOrWhiteSpace(_options.UserAgent) ? "LiveFuelMap/1.0" : _options.UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "application/json,text/plain,*/*");
        return request;
    }

    private static string AddQueryString(string endpoint, params (string Key, string Value)[] parameters)
    {
        var separator = endpoint.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var query = string.Join("&", parameters.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
        return $"{endpoint.TrimEnd('?', '&')}{separator}{query}";
    }

    private readonly record struct GeoPoint(double Lat, double Lon, string DisplayName);
    private readonly record struct RouteInfo(double DistanceKm, double DurationSeconds);
    private readonly record struct UsdRate(decimal Rate, string Date);
    private sealed record SearchSnippet(string Source, string Text, string? Url);
}
