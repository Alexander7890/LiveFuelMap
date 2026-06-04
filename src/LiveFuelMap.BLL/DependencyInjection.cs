using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.BLL.Services;
using LiveFuelMap.DAL.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LiveFuelMap.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IFuelDataService, FuelDataService>();
        services.AddScoped<IFuelPriceReportService, FuelPriceReportService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IStationAdminService, StationAdminService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IPriceChangeEmailNotifier, PriceChangeEmailNotifier>();
        services.AddScoped<ISubscriptionEmailNotifier, PriceChangeEmailNotifier>();
        services.AddScoped<IApiTokenService, ApiTokenService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IDataSourceService, DataSourceService>();
        services.AddScoped<IFuelPriceImportService, ValidatedFuelPriceImportService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IChatContextService, ChatContextService>();
        services.AddSingleton<IChatIntentRecognitionService, ChatIntentRecognitionService>();
        services.TryAddScoped<IExternalAutomotiveContextService, NoopExternalAutomotiveContextService>();
        services.AddSingleton<IChatTopicGuard, ChatTopicGuard>();
        services.AddSingleton<IFuelNormalizer, UnicodeFuelNormalizer>();
        services.AddSingleton<IPriceChangeDetector, PriceChangeDetector>();
        return services;
    }
}
