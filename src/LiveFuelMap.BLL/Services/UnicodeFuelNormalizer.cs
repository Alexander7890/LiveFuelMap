using System.Text.RegularExpressions;
using LiveFuelMap.BLL.Interfaces;

namespace LiveFuelMap.BLL.Services;

public sealed class UnicodeFuelNormalizer : IFuelNormalizer
{
    public string NormalizeStationKey(string stationName)
    {
        var lower = Transliterate(stationName.Trim().ToLowerInvariant());
        lower = Regex.Replace(lower, @"[\s_]+", "-");
        lower = Regex.Replace(lower, @"[^\p{L}\p{Nd}-]+", string.Empty);
        return Regex.Replace(lower, "-{2,}", "-").Trim('-');
    }

    public string NormalizeFuelCode(string fuelName)
    {
        var raw = fuelName.Trim().ToLowerInvariant();
        if (raw.Contains("дп") || raw.Contains("диз") || raw.Contains("diesel")) return "diesel";
        if (raw.Contains("газ") || raw.Contains("gas")) return "gas";

        var value = Transliterate(raw)
            .Replace("-", " ")
            .Replace("+", " plus")
            .Replace("а", "a");

        value = Regex.Replace(value, @"\s+", " ");

        if (value.Contains("95") && (value.Contains("plus") || raw.Contains("прем"))) return "a95plus";
        if (value.Contains("95")) return "a95";
        if (value.Contains("92")) return "a92";
        return value.Replace(" ", string.Empty);
    }

    private static string Transliterate(string value)
    {
        var map = new Dictionary<char, string>
        {
            ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "h", ['ґ'] = "g", ['д'] = "d",
            ['е'] = "e", ['є'] = "ie", ['ж'] = "zh", ['з'] = "z", ['и'] = "y", ['і'] = "i",
            ['ї'] = "i", ['й'] = "i", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n",
            ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
            ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "shch",
            ['ь'] = "", ['ю'] = "iu", ['я'] = "ia", ['ы'] = "y", ['э'] = "e", ['ё'] = "io",
            ['ъ'] = ""
        };

        return string.Concat(value.Select(ch => map.TryGetValue(ch, out var replacement) ? replacement : ch.ToString()));
    }
}
