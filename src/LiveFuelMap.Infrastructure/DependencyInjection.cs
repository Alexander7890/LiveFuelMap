using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.BLL.Services;
using LiveFuelMap.Infrastructure.AI;
using LiveFuelMap.Infrastructure.Auth;
using LiveFuelMap.Infrastructure.Email;
using LiveFuelMap.Infrastructure.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LiveFuelMap.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<ParserOptions>(configuration.GetSection("Parser"));
        services.Configure<FrontendOptions>(configuration.GetSection("Frontend"));
        services.Configure<GoogleAuthOptions>(configuration.GetSection("GoogleAuth"));
        services.Configure<CaptchaOptions>(configuration.GetSection("Captcha"));
        services.Configure<AiOptions>(configuration.GetSection("Ai"));
        services.Configure<ChatOptions>(configuration.GetSection("Chat"));
        services.Configure<ExternalContextOptions>(configuration.GetSection("ExternalContext"));

        services.AddSingleton<BCryptPasswordHasher>();
        services.AddSingleton<IPasswordHasher>(sp => sp.GetRequiredService<BCryptPasswordHasher>());
        services.AddSingleton<Sha1HashService>();
        services.AddSingleton<ITokenHasher>(sp => sp.GetRequiredService<Sha1HashService>());
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IChatRateLimiter, InMemoryChatRateLimiter>();
        services.AddHttpClient<IGoogleOAuthClient, GoogleOAuthClient>();
        services.AddHttpClient<ICaptchaVerificationService, CaptchaVerificationService>();
        services.AddHttpClient<GroqAiChatClient>();
        services.AddScoped<IAiChatClient>(sp => sp.GetRequiredService<GroqAiChatClient>());
        services.AddHttpClient<ExternalAutomotiveContextService>();
        services.AddScoped<IExternalAutomotiveContextService>(sp => sp.GetRequiredService<ExternalAutomotiveContextService>());

        if (configuration.GetValue<bool>("Email:UseSmtp"))
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        else
            services.AddSingleton<IEmailSender, NoopEmailSender>();

        services.AddSingleton<ISubscriptionNotificationQueue, SubscriptionNotificationQueue>();
        services.AddHostedService<SubscriptionNotificationWorker>();
        services.AddHostedService<SubscriptionDigestWorker>();

        services.AddHttpClient<MinfinHtmlPriceSourceClient>();
        services.AddHttpClient<JsonPriceSourceClient>();
        services.AddScoped<IPriceSourceClientFactory, PriceSourceClientFactory>();
        services.AddScoped<IFuelParserService, FuelParserService>();
        services.AddHostedService<FuelPriceParsingWorker>();

        return services;
    }
}
