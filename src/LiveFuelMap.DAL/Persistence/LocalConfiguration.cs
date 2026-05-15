namespace LiveFuelMap.DAL.Persistence;

public static class DotEnvFile
{
    public static IReadOnlyDictionary<string, string> Load(params string?[] basePaths)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in EnumerateDotEnvPaths(basePaths))
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                    continue;

                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex <= 0)
                    continue;

                var key = trimmed[..separatorIndex].Trim();
                var value = trimmed[(separatorIndex + 1)..].Trim().Trim('"');
                values[key] = value;
            }
        }

        return values;
    }

    private static IEnumerable<string> EnumerateDotEnvPaths(IEnumerable<string?> basePaths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = basePaths
            .Concat([Directory.GetCurrentDirectory(), AppContext.BaseDirectory])
            .Where(path => !string.IsNullOrWhiteSpace(path));

        foreach (var candidate in candidates)
        {
            var directory = NormalizeDirectory(candidate!);
            while (!string.IsNullOrWhiteSpace(directory))
            {
                var path = Path.Combine(directory, ".env");
                if (File.Exists(path) && seen.Add(path))
                    yield return path;

                if (File.Exists(Path.Combine(directory, "LiveFuelMap.sln")) ||
                    File.Exists(Path.Combine(directory, "LiveFuelMap.slnx")))
                    break;

                directory = Directory.GetParent(directory)?.FullName;
            }
        }
    }

    private static string? NormalizeDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath);
    }
}

public static class ConnectionStringFactory
{
    public static string Resolve(string? configuredConnectionString = null, string? basePath = null)
    {
        var explicitConnection = FirstNonEmpty(
            Environment.GetEnvironmentVariable("LIVEFUELMAP_CONNECTION"),
            Environment.GetEnvironmentVariable("ConnectionStrings__Default"));

        if (!string.IsNullOrWhiteSpace(explicitConnection))
            return explicitConnection;

        var dotEnv = DotEnvFile.Load(basePath, Directory.GetCurrentDirectory());
        var host = FirstNonEmpty(Environment.GetEnvironmentVariable("DB_HOST"), Get(dotEnv, "DB_HOST"));
        var user = FirstNonEmpty(Environment.GetEnvironmentVariable("DB_USER"), Get(dotEnv, "DB_USER"));
        var password = FirstNonEmpty(Environment.GetEnvironmentVariable("DB_PASSWORD"), Environment.GetEnvironmentVariable("DB_PASS"), Get(dotEnv, "DB_PASSWORD"), Get(dotEnv, "DB_PASS"));
        var database = FirstNonEmpty(Environment.GetEnvironmentVariable("DB_NAME"), Get(dotEnv, "DB_NAME"));
        var port = FirstNonEmpty(Environment.GetEnvironmentVariable("DB_PORT"), Get(dotEnv, "DB_PORT"), "3306");

        if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(database))
        {
            return $"server={host};port={port};database={database};user={user};password={password};SslMode=None;AllowPublicKeyRetrieval=True;";
        }

        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
            return configuredConnectionString;

        return "server=localhost;port=3306;database=livefuelmap;user=root;password=change_me;SslMode=None;AllowPublicKeyRetrieval=True;";
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
