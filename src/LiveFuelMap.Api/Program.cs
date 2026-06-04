using System.Text;
using System.Text.Json.Serialization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using LiveFuelMap.Api.Authentication;
using LiveFuelMap.Api.Hubs;
using LiveFuelMap.Api.Middleware;
using LiveFuelMap.Api.Security;
using LiveFuelMap.BLL;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.DAL.Persistence;
using LiveFuelMap.Infrastructure;
using LiveFuelMap.Infrastructure.Auth;
using LiveFuelMap.Infrastructure.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("LIVEFUELMAP_");
var solutionRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", ".."));
var dotEnv = DotEnvFile.Load(solutionRoot, Directory.GetCurrentDirectory());
var dotEnvConfiguration = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
foreach (var (key, value) in dotEnv)
{
    if (key.Contains("__", StringComparison.Ordinal))
        dotEnvConfiguration[key.Replace("__", ":")] = value;
}
if (dotEnv.TryGetValue("PORT", out var port))
{
    dotEnvConfiguration["Frontend:ApiBaseUrl"] = $"http://localhost:{port}";

    if (int.TryParse(port, out var portNumber) &&
        portNumber > 0 &&
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) &&
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_URLS")))
    {
        builder.WebHost.UseUrls($"http://localhost:{portNumber}");
    }
}
var dotEnvMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["JWT_SECRET"] = "Jwt:Secret",
    ["GOOGLE_CLIENT_ID"] = "GoogleAuth:ClientId",
    ["GOOGLE_CLIENT_SECRET"] = "GoogleAuth:ClientSecret",
    ["GOOGLE_REDIRECT_URI"] = "GoogleAuth:RedirectUri",
    ["GOOGLE_FRONTEND_CALLBACK_URL"] = "GoogleAuth:FrontendCallbackUrl",
    ["CAPTCHA_ENABLED"] = "Captcha:Enabled",
    ["CAPTCHA_PROVIDER"] = "Captcha:Provider",
    ["CAPTCHA_SITE_KEY"] = "Captcha:SiteKey",
    ["CAPTCHA_SECRET_KEY"] = "Captcha:SecretKey",
    ["CAPTCHA_VERIFY_ENDPOINT"] = "Captcha:VerifyEndpoint",
    ["AUTH_RATE_LIMIT_ENABLED"] = "AuthRateLimit:Enabled",
    ["AUTH_RATE_LIMIT_LOGIN_ATTEMPTS"] = "AuthRateLimit:LoginAttemptLimit",
    ["AUTH_RATE_LIMIT_REGISTER_ATTEMPTS"] = "AuthRateLimit:RegisterAttemptLimit",
    ["AUTH_RATE_LIMIT_WINDOW_SECONDS"] = "AuthRateLimit:WindowSeconds",
    ["AI_PROVIDER"] = "Ai:Provider",
    ["AI_MODEL"] = "Ai:Model",
    ["AI_ENDPOINT"] = "Ai:Endpoint",
    ["AI_TIMEOUT"] = "Ai:TimeoutSeconds",
    ["AI_TEMPERATURE"] = "Ai:Temperature",
    ["AI_MAX_TOKENS"] = "Ai:MaxTokens",
    ["GROQ_API_KEY"] = "Ai:ApiKey",
    ["GROQ_MODEL"] = "Ai:Model",
    ["GROQ_BASE_URL"] = "Ai:Endpoint",
    ["CHAT_RATE_LIMIT"] = "Chat:RateLimitPerMinute",
    ["CHAT_MAX_MESSAGE_LENGTH"] = "Chat:MaxMessageLength",
    ["API_RATE_LIMIT_ENABLED"] = "ApiSecurity:RateLimit:Enabled",
    ["API_RATE_LIMIT_READ_PER_MINUTE"] = "ApiSecurity:RateLimit:ReadPermitLimit",
    ["API_RATE_LIMIT_WRITE_PER_MINUTE"] = "ApiSecurity:RateLimit:WritePermitLimit",
    ["API_RATE_LIMIT_WINDOW_SECONDS"] = "ApiSecurity:RateLimit:WindowSeconds",
    ["API_RATE_LIMIT_QUEUE_LIMIT"] = "ApiSecurity:RateLimit:QueueLimit",
    ["IDEMPOTENCY_ENABLED"] = "ApiSecurity:Idempotency:Enabled",
    ["IDEMPOTENCY_HEADER_NAME"] = "ApiSecurity:Idempotency:HeaderName",
    ["IDEMPOTENCY_TTL_MINUTES"] = "ApiSecurity:Idempotency:KeyTtlMinutes",
    ["IDEMPOTENCY_MAX_BODY_BYTES"] = "ApiSecurity:Idempotency:MaxBodyBytes",
    ["EXTERNAL_CONTEXT_ENABLED"] = "ExternalContext:Enabled",
    ["EXTERNAL_CONTEXT_TIMEOUT"] = "ExternalContext:TimeoutSeconds",
    ["EXTERNAL_CONTEXT_USER_AGENT"] = "ExternalContext:UserAgent",
    ["NOMINATIM_ENDPOINT"] = "ExternalContext:NominatimEndpoint",
    ["OSRM_ENDPOINT"] = "ExternalContext:OsrmEndpoint",
    ["DUCKDUCKGO_ENDPOINT"] = "ExternalContext:DuckDuckGoEndpoint",
    ["DUCKDUCKGO_HTML_ENDPOINT"] = "ExternalContext:DuckDuckGoHtmlEndpoint",
    ["NBU_EXCHANGE_ENDPOINT"] = "ExternalContext:NbuExchangeEndpoint",
    ["MINFIN_FUEL_ENDPOINT"] = "ExternalContext:MinfinFuelEndpoint"
};
foreach (var (envKey, configurationKey) in dotEnvMappings)
{
    if (dotEnv.TryGetValue(envKey, out var value))
        dotEnvConfiguration[configurationKey] = value;

    var environmentValue = Environment.GetEnvironmentVariable(envKey);
    if (!string.IsNullOrWhiteSpace(environmentValue))
        dotEnvConfiguration[configurationKey] = environmentValue;
}
if (builder.Environment.IsEnvironment("Testing"))
{
    dotEnvConfiguration["ApiSecurity:RateLimit:Enabled"] = "false";
    dotEnvConfiguration["Captcha:Enabled"] = "false";
    dotEnvConfiguration["AuthRateLimit:Enabled"] = "false";
}
if (dotEnvConfiguration.Count > 0)
    builder.Configuration.AddInMemoryCollection(dotEnvConfiguration);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(builder.Environment.ContentRootPath, "logs", "livefuelmap.log")));
}

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
var jwtKeyMaterial = JwtSigningKeyFactory.Create(jwtOptions);
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".data-protection-keys"))));

builder.Services.AddDbContext<LiveFuelMapDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? "server=localhost;port=3306;database=livefuelmap;user=root;password=change_me;";
    connectionString = ConnectionStringFactory.Resolve(connectionString, solutionRoot);

    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 41)));
});

builder.Services.AddBusinessLogic();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddLiveFuelMapApiSecurity(builder.Configuration);
builder.Services.Configure<AuthRateLimitOptions>(builder.Configuration.GetSection("AuthRateLimit"));
builder.Services.AddSingleton<IAuthAttemptRateLimiter, InMemoryAuthAttemptRateLimiter>();
builder.Services.AddSingleton<IApiMetrics, InMemoryApiMetrics>();
builder.Services.AddSingleton<LivePresenceTracker>();
builder.Services.AddScoped<IFuelUpdatesNotifier, SignalRFuelUpdatesNotifier>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("Content-Disposition")
            .SetIsOriginAllowed(_ => true);
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LiveFuelMap API",
        Version = "v1",
        Description = "ASP.NET Core API for fuel prices, subscriptions, parser sources, admin operations, JWT auth and API tokens."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityDefinition(ApiTokenAuthenticationDefaults.Scheme, new OpenApiSecurityScheme
    {
        Description = "Third-party API token. Use X-API-Token or Authorization: Bearer {token}.",
        Name = "X-API-Token",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = ApiTokenAuthenticationDefaults.Scheme
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = ApiTokenAuthenticationDefaults.Scheme } }] = []
    });
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = jwtKeyMaterial.ValidationKey,
            ValidAlgorithms = [jwtKeyMaterial.Algorithm],
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role"
        };
        options.SaveToken = false;
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].ToString();
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    path.StartsWithSegments("/hubs/fuel", StringComparison.OrdinalIgnoreCase))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                var userIdValue = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
                var tokenVersionValue = principal?.FindFirst("token_version")?.Value;

                if (!int.TryParse(userIdValue, out var userId) ||
                    !int.TryParse(tokenVersionValue, out var tokenVersion))
                {
                    context.Fail("Invalid JWT claims.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<LiveFuelMapDbContext>();
                var user = await db.Users
                    .AsNoTracking()
                    .Where(x => x.Id == userId)
                    .Select(x => new { x.TokenVersion, x.Role, x.IsDeleted })
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                if (user is null || user.IsDeleted || user.TokenVersion != tokenVersion)
                {
                    context.Fail("JWT has been revoked.");
                    return;
                }

                var role = principal?.FindFirstValue("role");
                if (!string.Equals(role, user.Role.ToString(), StringComparison.Ordinal))
                {
                    context.Fail("JWT role is no longer valid.");
                }
            }
        };
    })
    .AddScheme<ApiTokenAuthenticationOptions, ApiTokenAuthenticationHandler>(
        ApiTokenAuthenticationDefaults.Scheme,
        options => options.RequiredScope = "fuel:read")
    .AddScheme<ApiTokenAuthenticationOptions, ApiTokenAuthenticationHandler>(
        ApiTokenAuthenticationDefaults.CommentsReadScheme,
        options => options.RequiredScope = "comments:read")
    .AddScheme<ApiTokenAuthenticationOptions, ApiTokenAuthenticationHandler>(
        ApiTokenAuthenticationDefaults.FuelWriteScheme,
        options => options.RequiredScope = "fuel:write");

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ApiTokenFuelRead", policy =>
    {
        policy.AddAuthenticationSchemes(ApiTokenAuthenticationDefaults.Scheme);
        policy.RequireAuthenticatedUser();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

var frontendDistPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "frontend", "dist"));
var frontendPublicPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "frontend", "public"));
var frontendPath = Directory.Exists(frontendDistPath) ? frontendDistPath : frontendPublicPath;
var frontendUploadsPath = Path.Combine(frontendPublicPath, "uploads");
if (Directory.Exists(frontendUploadsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frontendUploadsPath),
        RequestPath = "/uploads"
    });
}

if (Directory.Exists(frontendPath))
{
    var fileProvider = new PhysicalFileProvider(frontendPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
}

app.UseCors("Frontend");
app.UseRateLimiter();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ApiMetricsMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<IdempotencyMiddleware>();

app.MapControllers();
app.MapHub<FuelHub>("/hubs/fuel").AllowAnonymous();
if (Directory.Exists(frontendPath))
{
    app.MapFallback(async context =>
    {
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var indexFile = Path.Combine(frontendPath, "index.html");
        if (!File.Exists(indexFile))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(indexFile);
    });
}

app.Run();

public partial class Program
{
}
