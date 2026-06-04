using LiveFuelMap.DAL.Enums;

namespace LiveFuelMap.BLL.DTOs;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record RegisterRequest(string Email, string Password, string ConfirmPassword, string DisplayName, string Nickname, string? CaptchaToken = null);
public sealed record LoginRequest(string Email, string Password, string? CaptchaToken = null);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest(string? RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);
public sealed record VerifyEmailRequest(string Code);
public sealed record GoogleCredentialLoginRequest(string Credential);
public sealed record GoogleAccountDto(string Email, string DisplayName, string? ProfileImageUrl, string ProviderUserId, bool EmailVerified);
public sealed record JwtAccessTokenDto(string Token, DateTime ExpiresAt);
public sealed record AuthResultDto(
    int UserId,
    string Email,
    string Role,
    string Token,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    string TokenType = "Bearer",
    bool RequiresNickname = false,
    string? SuggestedNickname = null,
    string AuthProvider = "Local");
public sealed record CurrentUserDto(
    int UserId,
    string Email,
    string Role,
    bool EmailConfirmed,
    string DisplayName,
    string Nickname,
    string ProfileImageUrl,
    bool CanSubscribeToEmail,
    string AuthProvider,
    bool RequiresNicknameSetup);

public sealed record ProfileDto(
    int UserId,
    string Email,
    string Role,
    string DisplayName,
    string Nickname,
    string ProfileImageUrl,
    bool EmailConfirmed,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    DateTime? VerificationCodeExpiresAt,
    DateTime? VerificationRetryAfter,
    string AuthProvider,
    bool RequiresNicknameSetup);

public sealed record UpdateProfileRequest(string? DisplayName, string? Nickname, string? ProfileImageUrl);
public sealed record SetupNicknameRequest(string Nickname);
public sealed record DeleteAccountRequest(string? Email = null, string? Password = null, string? VerificationCode = null, string? ConfirmText = null);
public sealed record DeleteAccountVerificationRequest(string? Email = null);

public sealed record FuelDto(int Id, string Code, string Name, int SortOrder);
public sealed record FuelPriceDto(int FuelId, string FuelCode, string FuelName, decimal Price, int Popularity, DateTime Date);
public sealed record PriceChangeNotificationDto(
    int StationId,
    string StationName,
    string StationCity,
    int FuelId,
    string FuelCode,
    string FuelName,
    decimal? OldPrice,
    decimal NewPrice,
    string ChangeType,
    DateTime Date);

public sealed record StationDto(
    int Id,
    string Name,
    string Address,
    string City,
    decimal Latitude,
    decimal Longitude,
    string ImageUrl,
    string? WebsiteUrl,
    IReadOnlyList<string> PhotoUrls,
    decimal? AverageRating,
    int ReviewCount,
    IReadOnlyList<FuelPriceDto> Prices,
    IReadOnlyDictionary<string, decimal?> PricesByFuelCode);

public sealed record StationListQuery(string? City, string? Name, string? FuelCode, int Page = 1, int PageSize = 50);
public sealed record UpsertStationRequest(string Name, string Address, string City, decimal Latitude, decimal Longitude, string? ImageUrl, string? WebsiteUrl, IReadOnlyList<string>? PhotoUrls);
public sealed record UpsertFuelPriceRequest(int StationId, int FuelId, decimal Price, int Popularity, DateTime Date);
public sealed record FuelPriceCorrectionResultDto(FuelPriceDto Price, PriceChangeNotificationDto? Change);

public sealed record CommentDto(
    int Id,
    int UserId,
    int StationId,
    string StationName,
    int? FuelId,
    string? FuelName,
    string AuthorName,
    string Content,
    int Rating,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CommentAdminQuery(
    string? Search = null,
    string? Author = null,
    string? Status = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    int Page = 1,
    int PageSize = 20);

public sealed record AdminCommentDto(
    int Id,
    int UserId,
    string AuthorEmail,
    string AuthorNickname,
    string AuthorName,
    string AuthorType,
    int StationId,
    string StationName,
    int? FuelId,
    string? FuelName,
    string Content,
    int Rating,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string Status);

public sealed record CreateCommentRequest(int StationId, int? FuelId, string Content, int? Rating = null);
public sealed record UpdateCommentRequest(string Content, int? Rating = null);
public sealed record RealtimePresenceDto(int TotalConnections, int AnonymousConnections, int AuthenticatedUsers);

public sealed record SubscriptionRequest(
    string City,
    SubscriptionFrequency Frequency = SubscriptionFrequency.Daily,
    int? FuelId = null,
    IReadOnlyList<int>? FuelIds = null,
    string? SendTime = null,
    string? Email = null,
    bool IsActive = true);

public sealed record SubscriptionDto(
    int Id,
    int FuelId,
    string FuelCode,
    string FuelName,
    string City,
    SubscriptionFrequency Frequency,
    string SendTime,
    string Email,
    bool IsActive,
    DateTime? LastSentAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CompareRequest(IReadOnlyList<int> StationIds);
public sealed record CompareStationDto(int Id, string Name, string Address, string City, decimal Latitude, decimal Longitude, IReadOnlyDictionary<int, FuelPriceDto> Prices);
public sealed record CompareResultDto(IReadOnlyList<FuelDto> Fuels, IReadOnlyList<CompareStationDto> Stations);

public sealed record PriceHistoryQuery(string? Fuel = "all", DateTime? From = null, DateTime? To = null, int? StationId = null);
public sealed record ChartDatasetDto(string Label, IReadOnlyList<decimal?> Data, string BorderColor, decimal Tension = 0.4m);
public sealed record PriceHistoryDto(IReadOnlyList<string> Labels, IReadOnlyList<ChartDatasetDto> Datasets);

public sealed record FuelPriceExportRequest(
    string? City,
    IReadOnlyList<int>? StationIds,
    IReadOnlyList<string>? FuelCodes,
    DateTime From,
    DateTime To,
    string Format);

public sealed record FuelPriceExportFile(string FileName, string ContentType, byte[] Content);

public sealed record ApiTokenCreateRequest(string Name, string Scopes, DateTime? ExpiresAt);
public sealed record ApiTokenCreatedDto(int Id, string Name, string Token, string Scopes, DateTime? ExpiresAt);
public sealed record ApiTokenDto(int Id, string Name, string Scopes, DateTime CreatedAt, DateTime? ExpiresAt, DateTime? RevokedAt, int CreatedByUserId, string CreatedByEmail, string Status);

public sealed record ApiTokenQuery(
    string? Search = null,
    string? Status = null,
    string? Scope = null,
    string? UserEmail = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    DateTime? ExpiresFrom = null,
    DateTime? ExpiresTo = null,
    int Page = 1,
    int PageSize = 20);

public sealed record UserAdminQuery(
    string? Search = null,
    UserRole? Role = null,
    string? Provider = null,
    string? Status = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    int Page = 1,
    int PageSize = 20);

public sealed record UserAdminDto(
    int Id,
    string Email,
    string Role,
    bool EmailConfirmed,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    string DisplayName,
    string Nickname,
    string AuthProvider,
    bool IsDeleted,
    DateTime? DeletedAt,
    string Status);
public sealed record UpdateUserRoleRequest(UserRole Role);

public sealed record DataSourceDto(int Id, string Name, string Url, DataSourceType Type, bool Enabled, DateTime? LastSuccessAt);
public sealed record UpsertDataSourceRequest(string Name, string Url, DataSourceType Type, bool Enabled);
public sealed record ParserRunDto(int Id, int SourceId, string SourceName, DateTime StartedAt, DateTime? FinishedAt, string Status, int RecordsFound, int RecordsSaved, string? Error);

public sealed record FuelPriceRecord(string StationName, string City, string FuelName, decimal Price, DateTime Date, string? Address = null, decimal? Latitude = null, decimal? Longitude = null);
public sealed record FuelImportResult(int RecordsFound, int RecordsSaved, IReadOnlyList<PriceChangeNotificationDto>? PriceChanges = null);

public sealed record ApiUsageMetricDto(string Controller, long Count, double AverageMilliseconds);

public sealed record ChatRequest(string Message, string? SessionId = null, string? City = null, string? FuelCode = null, int? StationId = null, decimal? Latitude = null, decimal? Longitude = null, string? Language = null);
public sealed record ChatResponseDto(string Answer, string SessionId, string Intent, string Status, DateTime CreatedAt, ChatStructuredDataDto? Data = null);
public sealed record ChatHistoryDto(int Id, string SessionId, int? UserId, string Message, string Answer, string Intent, string Status, DateTime CreatedAt);

public sealed record ChatTopicDecision(bool IsAllowed, string Intent, bool IsSecurityBlocked = false);
public sealed record ChatContextResult(bool HasRequiredData, bool RequiresFuelData, string Intent, string Context, bool UsesExternalContext = false, string? DirectAnswer = null);
public sealed record ChatIntentAnalysisDto(
    string Intent,
    string Category,
    string? FuelCode = null,
    string? StationHint = null,
    decimal? Liters = null,
    bool RequiresDatabase = false,
    bool RequiresLocation = false,
    bool UsesConversationContext = false);

public sealed record FuelPriceResponseDto(string Station, string Fuel, decimal Price, DateTime Date, string Source = "LiveFuelMap");
public sealed record StationDistanceResponseDto(string Station, string Address, string City, double DistanceKm, string? FuelCode = null, decimal? Price = null);
public sealed record FuelCostCalculationResponseDto(string Fuel, decimal Liters, decimal PricePerLiter, decimal TotalCost, string Basis, string Source = "LiveFuelMap");
public sealed record FuelConsumptionCalculationResponseDto(decimal DistanceKm, decimal ConsumptionLitersPer100Km, decimal RequiredLiters);
public sealed record FuelStatisticsResponseDto(string Fuel, decimal? MinPrice, decimal? AveragePrice, decimal? MaxPrice, int StationCount, DateTime? Date);
public sealed record FuelHistoryPointDto(string Station, string Fuel, decimal Price, DateTime Date);
public sealed record StationFuelComparisonDto(string Station, IReadOnlyList<FuelPriceResponseDto> Prices, decimal? AveragePrice = null);
public sealed record ChatStructuredDataDto(
    string Type,
    IReadOnlyList<FuelPriceResponseDto>? FuelPrices = null,
    IReadOnlyList<StationDistanceResponseDto>? Stations = null,
    FuelCostCalculationResponseDto? FuelCost = null,
    FuelConsumptionCalculationResponseDto? FuelConsumption = null,
    FuelStatisticsResponseDto? Statistics = null,
    IReadOnlyList<FuelHistoryPointDto>? History = null,
    IReadOnlyList<StationFuelComparisonDto>? StationComparisons = null);
