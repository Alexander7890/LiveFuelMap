using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;

namespace LiveFuelMap.BLL.Services;

public sealed class ChatIntentRecognitionService : IChatIntentRecognitionService
{
    private static readonly HashSet<string> SupportedFuelCodes = ["a95plus", "a95", "a92", "diesel", "gas"];

    public ChatIntentAnalysisDto Analyze(ChatRequest request, string message, bool usesConversationContext = false)
    {
        var normalized = Normalize(message, keepPlus: true);
        var fuelCode = DetectFuelCode(message) ?? NormalizeFuelCode(request.FuelCode);
        var liters = DetectLiters(message);
        var stationHint = DetectStationHint(message);
        var hasStationServiceSignal = ContainsAny(normalized,
        [
            "pidkach", "pidkack", "shyn", "shin", "koles", "kompresor", "tsilodob", "cilodob", "24", "pratsiuiut", "praciuut", "works"
        ]);

        var hasFuelSignal = fuelCode is not null || ContainsAny(normalized,
        [
            "palne", "palyvo", "palyv", "fuel", "petrol", "gasoline", "benz", "benzin", "benzyn", "diesel", "dyzel", "dizel", "gaz", "gas", "lpg"
        ]);
        var hasPriceSignal = ContainsAny(normalized,
        [
            "tsina", "cina", "price", "cost", "vartist", "koshtu", "skilky", "skolko", "deshev", "dorozh", "naidesh", "nainyzh", "top", "top5"
        ]);
        var hasComparisonSignal = ContainsAny(normalized,
        [
            "porivn", "sravni", "compare", "mizh", "mezhdu", "riznyts", "raznits"
        ]);
        var hasHistorySignal = ContainsAny(normalized,
        [
            "istori", "history", "trend", "tendents", "dynamik", "zminyl", "zmin", "misiats", "misyac", "rik", "year", "grafik", "chart"
        ]);
        var hasStatisticsSignal = ContainsAny(normalized,
        [
            "seredn", "sredn", "average", "avg", "statyst", "statist", "minimum", "maximum", "top", "top5", "naidesh", "nainyzh", "usi", "vsi", "vsikh", "vsip", "vsiazk"
        ]);
        var hasNearestSignal = ContainsAny(normalized,
        [
            "poruch", "poblyz", "blyzko", "near", "nearby", "nearest", "nablyzh", "naiblyzh", "najblyzh", "geolok", "geo", "radius"
        ]);
        var hasRecommendationSignal = ContainsAny(normalized,
        [
            "vyhidn", "vygodn", "recommend", "rekomend", "krashch", "luchshe", "dekrashche", "naivygidn", "najvig", "naivyg"
        ]);
        var hasVehicleSignal = ContainsAny(normalized,
        [
            "toyota", "camry", "volkswagen", "golf", "tsi", "tdi", "engine", "dvygun", "dvyhun", "oktan", "octane", "vitrata", "vytrata", "rashod", "rozkhid", "roshid", "consumption", "zym", "winter", "zmishuv", "zmishuvat", "smeshat", "a92ta95", "a95plus"
        ]);

        if (hasStationServiceSignal)
        {
            return new ChatIntentAnalysisDto(
                "nearest_station",
                "nearest_station",
                fuelCode,
                stationHint,
                liters,
                RequiresDatabase: true,
                RequiresLocation: hasNearestSignal,
                UsesConversationContext: usesConversationContext);
        }

        if (hasNearestSignal)
        {
            return new ChatIntentAnalysisDto(
                "nearest_station",
                "nearest_station",
                fuelCode,
                stationHint,
                liters,
                RequiresDatabase: true,
                RequiresLocation: true,
                UsesConversationContext: usesConversationContext);
        }

        if (hasHistorySignal && hasFuelSignal)
        {
            return new ChatIntentAnalysisDto(
                "fuel_history",
                "fuel_history",
                fuelCode,
                stationHint,
                liters,
                RequiresDatabase: true,
                UsesConversationContext: usesConversationContext);
        }

        if ((hasComparisonSignal || stationHint is not null && ContainsAny(normalized, ["deshevshe", "dorozhche", "cheaper", "costlier"])) &&
            (hasFuelSignal || stationHint is not null))
        {
            var category = stationHint is not null ? "station_comparison" : "fuel_comparison";
            return new ChatIntentAnalysisDto(category, category, fuelCode, stationHint, liters, true, UsesConversationContext: usesConversationContext);
        }

        if (hasStatisticsSignal && hasFuelSignal)
        {
            return new ChatIntentAnalysisDto("fuel_statistics", "fuel_statistics", fuelCode, stationHint, liters, true, UsesConversationContext: usesConversationContext);
        }

        if (hasRecommendationSignal && hasFuelSignal)
        {
            return new ChatIntentAnalysisDto("fuel_recommendation", "fuel_recommendation", fuelCode, stationHint, liters, true, UsesConversationContext: usesConversationContext);
        }

        if ((hasPriceSignal || liters is not null) && hasFuelSignal)
        {
            return new ChatIntentAnalysisDto("fuel_price", "fuel_price", fuelCode, stationHint, liters, true, UsesConversationContext: usesConversationContext);
        }

        if (hasVehicleSignal)
        {
            return new ChatIntentAnalysisDto("vehicle_question", "vehicle_question", fuelCode, stationHint, liters, RequiresDatabase: false, UsesConversationContext: usesConversationContext);
        }

        return new ChatIntentAnalysisDto("general_chat", "general_chat", fuelCode, stationHint, liters, UsesConversationContext: usesConversationContext);
    }

    public static string Normalize(string value, bool keepPlus = false)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (TryTransliterate(ch, out var replacement))
            {
                builder.Append(replacement);
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                continue;
            }

            if (keepPlus && ch == '+')
                builder.Append('+');
        }

        return builder.ToString();
    }

    public static string? DetectFuelCode(string message)
    {
        var text = Normalize(message, keepPlus: true);

        if (ContainsAny(text, ["a95+", "ai95+", "ay95+", "95+", "a95plus", "ai95plus", "ay95plus", "pulls95", "mustang95", "premium95"]))
            return "a95plus";
        if (ContainsAny(text, ["diesel", "dyzel", "dizel", "dp", "dt", "dppalne", "dizpalne"]))
            return "diesel";
        if (Regex.IsMatch(text, @"(?<!\d)95(?!\d|\+)") || ContainsAny(text, ["a95", "ai95", "ay95", "benzin95", "benzyn95"]))
            return "a95";
        if (Regex.IsMatch(text, @"(?<!\d)92(?!\d)") || ContainsAny(text, ["a92", "ai92", "ay92", "benzin92", "benzyn92"]))
            return "a92";
        if (ContainsAny(text, ["lpg", "gaz", "gas", "avtogaz", "avtohaz"]) || Regex.IsMatch(text, @"(?<!k)haz", RegexOptions.CultureInvariant))
            return "gas";

        return null;
    }

    public static decimal? DetectLiters(string message)
    {
        var match = Regex.Match(
            message,
            @"(?<!\d)(\d+(?:[,.]\d+)?)\s*(?:л\.?|літр(?:ів|и|а)?|литр(?:ов|а|ы)?|l\b)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

        if (!match.Success)
            return null;

        var value = match.Groups[1].Value.Replace(',', '.');
        return decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var liters) && liters > 0
            ? liters
            : null;
    }

    private static string? NormalizeFuelCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var code = value.Trim().ToLowerInvariant();
        return SupportedFuelCodes.Contains(code) ? code : DetectFuelCode(value);
    }

    private static string? DetectStationHint(string message)
    {
        var text = Normalize(message);
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["okko"] = "OKKO",
            ["oko"] = "OKKO",
            ["wog"] = "WOG",
            ["vog"] = "WOG",
            ["voh"] = "WOG",
            ["brsm"] = "БРСМ",
            ["brsmnafta"] = "БРСМ",
            ["upg"] = "UPG",
            ["iupg"] = "UPG",
            ["yupg"] = "UPG",
            ["socar"] = "SOCAR",
            ["sokar"] = "SOCAR",
            ["shell"] = "Shell",
            ["brentoil"] = "Brent Oil",
            ["brandoil"] = "Brent Oil",
            ["brendoil"] = "Brent Oil",
            ["brent"] = "Brent Oil"
        };

        return aliases.FirstOrDefault(x => text.Contains(x.Key, StringComparison.Ordinal)).Value;
    }

    private static bool ContainsAny(string text, IEnumerable<string> terms) =>
        terms.Any(term => text.Contains(term, StringComparison.Ordinal));

    private static bool TryTransliterate(char ch, out string replacement)
    {
        replacement = ch switch
        {
            'а' => "a",
            'б' => "b",
            'в' => "v",
            'г' => "h",
            'ґ' => "g",
            'д' => "d",
            'е' => "e",
            'є' => "ie",
            'ё' => "e",
            'ж' => "zh",
            'з' => "z",
            'и' => "y",
            'і' => "i",
            'ї' => "i",
            'й' => "i",
            'к' => "k",
            'л' => "l",
            'м' => "m",
            'н' => "n",
            'о' => "o",
            'п' => "p",
            'р' => "r",
            'с' => "s",
            'т' => "t",
            'у' => "u",
            'ф' => "f",
            'х' => "kh",
            'ц' => "ts",
            'ч' => "ch",
            'ш' => "sh",
            'щ' => "shch",
            'ь' => "",
            'ы' => "y",
            'ъ' => "",
            'э' => "e",
            'ю' => "iu",
            'я' => "ia",
            _ => string.Empty
        };

        return replacement.Length > 0 || ch is 'ь' or 'ъ';
    }
}
