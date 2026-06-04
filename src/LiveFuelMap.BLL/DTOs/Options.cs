namespace LiveFuelMap.BLL.DTOs;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "LiveFuelMap";
    public string Audience { get; set; } = "LiveFuelMap.Frontend";
    public string Algorithm { get; set; } = "HS256";
    public string Secret { get; set; } = string.Empty;
    public bool SecretIsBase64 { get; set; } = false;
    public string? PrivateKeyPath { get; set; }
    public string? PublicKeyPath { get; set; }
    public string? PrivateKeyPem { get; set; }
    public string? PublicKeyPem { get; set; }
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 7;
}

public sealed class ParserOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
    public int IntervalSeconds { get; set; } = 300;
    public int RetentionDays { get; set; } = 90;
    public bool RunOnStartup { get; set; } = false;
}

public sealed class FrontendOptions
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5000";
    public string GoogleMapsApiKey { get; set; } = string.Empty;
}

public sealed class GoogleAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "http://localhost:5000/api/auth/google/callback";
    public string FrontendCallbackUrl { get; set; } = "http://localhost:5173/auth/google/callback";
    public string AuthorizationEndpoint { get; set; } = "https://accounts.google.com/o/oauth2/v2/auth";
    public string TokenEndpoint { get; set; } = "https://oauth2.googleapis.com/token";
    public int StateLifetimeMinutes { get; set; } = 10;
}

public sealed class CaptchaOptions
{
    public bool Enabled { get; set; } = false;
    public string Provider { get; set; } = "RecaptchaV2";
    public string SiteKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string VerifyEndpoint { get; set; } = "https://www.google.com/recaptcha/api/siteverify";
}

public sealed class AuthRateLimitOptions
{
    public bool Enabled { get; set; } = true;
    public int LoginAttemptLimit { get; set; } = 8;
    public int RegisterAttemptLimit { get; set; } = 5;
    public int WindowSeconds { get; set; } = 300;
}

public sealed class AiOptions
{
    public string Provider { get; set; } = "Groq";
    public string Model { get; set; } = "llama-3.1-8b-instant";
    public string Endpoint { get; set; } = "https://api.groq.com/openai/v1/chat/completions";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public decimal Temperature { get; set; } = 0.4m;
    public int MaxTokens { get; set; } = 400;
}

public sealed class ChatOptions
{
    public int RateLimitPerMinute { get; set; } = 20;
    public int MaxMessageLength { get; set; } = 1000;
}

public sealed class ApiSecurityOptions
{
    public ApiRateLimitOptions RateLimit { get; set; } = new();
    public IdempotencyOptions Idempotency { get; set; } = new();
}

public sealed class ApiRateLimitOptions
{
    public bool Enabled { get; set; } = true;
    public int ReadPermitLimit { get; set; } = 120;
    public int WritePermitLimit { get; set; } = 40;
    public int WindowSeconds { get; set; } = 60;
    public int QueueLimit { get; set; } = 20;
}

public sealed class IdempotencyOptions
{
    public bool Enabled { get; set; } = true;
    public string HeaderName { get; set; } = "Idempotency-Key";
    public int KeyTtlMinutes { get; set; } = 60;
    public int MaxKeyLength { get; set; } = 128;
    public int MaxBodyBytes { get; set; } = 1_048_576;
}

public sealed class ExternalContextOptions
{
    public bool Enabled { get; set; } = true;
    public string NominatimEndpoint { get; set; } = "https://nominatim.openstreetmap.org/search";
    public string OsrmEndpoint { get; set; } = "https://router.project-osrm.org";
    public string DuckDuckGoEndpoint { get; set; } = "https://api.duckduckgo.com/";
    public string DuckDuckGoHtmlEndpoint { get; set; } = "https://html.duckduckgo.com/html/";
    public string NbuExchangeEndpoint { get; set; } = "https://bank.gov.ua/NBUStatService/v1/statdirectory/exchange";
    public string MinfinFuelEndpoint { get; set; } = "https://index.minfin.com.ua/ua/markets/fuel/reg/harkovskaya/";
    public string UserAgent { get; set; } = "LiveFuelMap/1.0 local diploma project";
    public int TimeoutSeconds { get; set; } = 15;
}
