using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.DAL.Entities;
using LiveFuelMap.DAL.Enums;

namespace LiveFuelMap.BLL.Interfaces;

public interface IPasswordHasher
{
    string Hash(string value);
    bool Verify(string value, string hash);
}

public interface ITokenHasher
{
    string Hash(string value);
    bool Verify(string value, string hash);
}

public interface ITokenService
{
    JwtAccessTokenDto CreateAccessToken(User user);
}

public interface IEmailSender
{
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = false, CancellationToken cancellationToken = default);
}

public interface IAuthService
{
    Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResultDto> LoginWithGoogleAsync(GoogleAccountDto googleAccount, CancellationToken cancellationToken = default);
    Task<AuthResultDto> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(int userId, LogoutRequest request, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task VerifyEmailAsync(int userId, VerifyEmailRequest request, CancellationToken cancellationToken = default);
    Task ResendEmailVerificationCodeAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> SuggestNicknamesAsync(string nickname, int count = 3, CancellationToken cancellationToken = default);
    Task<CurrentUserDto> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default);
}

public interface IGoogleOAuthClient
{
    string CreateAuthorizationUrl(string? returnUrl = null);
    string CreateFrontendCallbackUrl(AuthResultDto result, string? returnUrl = null);
    string CreateFrontendErrorUrl(string error, string? returnUrl = null);
    Task<(GoogleAccountDto Account, string? ReturnUrl)> ExchangeCodeAsync(string code, string state, CancellationToken cancellationToken = default);
    Task<GoogleAccountDto> ValidateCredentialAsync(string credential, CancellationToken cancellationToken = default);
}

public interface ICaptchaVerificationService
{
    Task VerifyAsync(string? captchaToken, CancellationToken cancellationToken = default);
}

public interface IProfileService
{
    Task<ProfileDto> GetAsync(int userId, CancellationToken cancellationToken = default);
    Task<ProfileDto> SetupNicknameAsync(int userId, SetupNicknameRequest request, CancellationToken cancellationToken = default);
    Task<ProfileDto> UpdateAsync(int userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task RequestDeletionCodeAsync(int userId, DeleteAccountVerificationRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int userId, DeleteAccountRequest request, CancellationToken cancellationToken = default);
}

public interface IFuelDataService
{
    Task<IReadOnlyList<FuelDto>> GetFuelsAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<StationDto>> GetStationsAsync(StationListQuery query, CancellationToken cancellationToken = default);
    Task<StationDto?> GetStationAsync(int id, CancellationToken cancellationToken = default);
    Task<PriceHistoryDto> GetPriceHistoryAsync(PriceHistoryQuery query, CancellationToken cancellationToken = default);
    Task<CompareResultDto> CompareStationsAsync(IReadOnlyList<int> stationIds, CancellationToken cancellationToken = default);
    Task<FuelPriceCorrectionResultDto> CorrectFuelPriceAsync(UpsertFuelPriceRequest request, CancellationToken cancellationToken = default);
}

public interface IFuelPriceReportService
{
    Task<FuelPriceExportFile> ExportAsync(FuelPriceExportRequest request, CancellationToken cancellationToken = default);
}

public interface ICommentService
{
    Task<PagedResult<AdminCommentDto>> ListAdminAsync(CommentAdminQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommentDto>> ListAsync(int? stationId = null, string? search = null, int take = 200, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommentDto>> GetByStationAsync(int stationId, CancellationToken cancellationToken = default);
    Task<CommentDto> CreateAsync(int userId, CreateCommentRequest request, CancellationToken cancellationToken = default);
    Task<CommentDto?> UpdateAsync(int id, UpdateCommentRequest request, CancellationToken cancellationToken = default);
    Task<CommentDto?> UpdateOwnAsync(int userId, int id, UpdateCommentRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<CommentDto?> DeleteOwnAsync(int userId, int id, CancellationToken cancellationToken = default);
}

public interface IStationAdminService
{
    Task<StationDto> CreateAsync(UpsertStationRequest request, CancellationToken cancellationToken = default);
    Task<StationDto?> UpdateAsync(int id, UpsertStationRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public interface ISubscriptionService
{
    Task<IReadOnlyList<SubscriptionDto>> ListAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubscriptionDto>> CreateAsync(int userId, SubscriptionRequest request, CancellationToken cancellationToken = default);
    Task<SubscriptionDto?> UpdateAsync(int userId, int id, SubscriptionRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int userId, int id, CancellationToken cancellationToken = default);
}

public interface IApiTokenService
{
    Task<ApiTokenCreatedDto> CreateAsync(int adminUserId, ApiTokenCreateRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<ApiTokenDto>> ListAsync(ApiTokenQuery query, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ValidateAsync(string token, string requiredScope, CancellationToken cancellationToken = default);
}

public interface IUserAdminService
{
    Task<PagedResult<UserAdminDto>> ListAsync(UserAdminQuery query, CancellationToken cancellationToken = default);
    Task<UserAdminDto?> UpdateRoleAsync(int userId, UserRole role, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int userId, int currentAdminUserId, CancellationToken cancellationToken = default);
}

public interface IDataSourceService
{
    Task<IReadOnlyList<DataSourceDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<DataSourceDto> CreateAsync(UpsertDataSourceRequest request, CancellationToken cancellationToken = default);
    Task<DataSourceDto?> UpdateAsync(int id, UpsertDataSourceRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParserRunDto>> GetParserRunsAsync(CancellationToken cancellationToken = default);
}

public interface IFuelNormalizer
{
    string NormalizeStationKey(string stationName);
    string NormalizeFuelCode(string fuelName);
}

public interface IPriceChangeDetector
{
    bool IsMeaningfulChange(decimal? latestPrice, decimal newPrice);
}

public interface IFuelPriceImportService
{
    Task<FuelImportResult> ImportAsync(int sourceId, IReadOnlyList<FuelPriceRecord> records, CancellationToken cancellationToken = default);
}

public interface IFuelParserService
{
    Task<IReadOnlyList<ParserRunDto>> RunOnceAsync(CancellationToken cancellationToken = default);
}

public interface IPriceSourceClient
{
    Task<IReadOnlyList<FuelPriceRecord>> FetchAsync(DataSource source, CancellationToken cancellationToken = default);
}

public interface IPriceSourceClientFactory
{
    IPriceSourceClient Create(DataSource source);
}

public interface IFuelUpdatesNotifier
{
    Task NotifyFuelDataUpdatedAsync(CancellationToken cancellationToken = default);
    Task NotifyFuelDataUpdatedAsync(IReadOnlyList<PriceChangeNotificationDto> priceChanges, CancellationToken cancellationToken = default);
}

public interface IPriceChangeEmailNotifier
{
    Task NotifyAsync(IReadOnlyList<PriceChangeNotificationDto> priceChanges, CancellationToken cancellationToken = default);
}

public interface ISubscriptionEmailNotifier
{
    Task NotifySubscriptionCreatedAsync(int userId, int subscriptionId, CancellationToken cancellationToken = default);
    Task NotifyDueScheduledAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

public sealed record SubscriptionNotificationJob(
    string Type,
    int? UserId = null,
    int? SubscriptionId = null,
    IReadOnlyList<PriceChangeNotificationDto>? PriceChanges = null);

public interface ISubscriptionNotificationQueue
{
    void QueueSubscriptionCreated(int userId, int subscriptionId);
    void QueuePriceChanges(IReadOnlyList<PriceChangeNotificationDto> priceChanges);
    ValueTask<SubscriptionNotificationJob> DequeueAsync(CancellationToken cancellationToken = default);
}

public interface IApiMetrics
{
    void Track(string controller, long elapsedMilliseconds);
    IReadOnlyList<ApiUsageMetricDto> Snapshot();
}

public interface IChatService
{
    Task<ChatResponseDto> AskAsync(ChatRequest request, int? userId, string ipAddress, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatHistoryDto>> GetHistoryAsync(string sessionId, int? userId, CancellationToken cancellationToken = default);
    Task ClearHistoryAsync(string sessionId, int? userId, CancellationToken cancellationToken = default);
}

public interface IChatTopicGuard
{
    ChatTopicDecision Check(string message);
}

public interface IChatContextService
{
    Task<ChatContextResult> BuildContextAsync(ChatRequest request, ChatTopicDecision topic, CancellationToken cancellationToken = default);
}

public interface IChatIntentRecognitionService
{
    ChatIntentAnalysisDto Analyze(ChatRequest request, string message, bool usesConversationContext = false);
}

public interface IExternalAutomotiveContextService
{
    Task<string> BuildContextAsync(ChatRequest request, ChatTopicDecision topic, CancellationToken cancellationToken = default);
    Task<string?> BuildDirectAnswerAsync(ChatRequest request, ChatTopicDecision topic, CancellationToken cancellationToken = default);
}

public interface IAiChatClient
{
    Task<string> CompleteAsync(string systemPrompt, string context, string userMessage, CancellationToken cancellationToken = default);
}

public interface IChatRateLimiter
{
    Task<bool> IsAllowedAsync(int? userId, string sessionId, string ipAddress, string message, CancellationToken cancellationToken = default);
}
