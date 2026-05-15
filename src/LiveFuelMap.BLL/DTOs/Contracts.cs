using LiveFuelMap.DAL.Enums;

namespace LiveFuelMap.BLL.DTOs;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record RegisterRequest(string Email, string Password, string ConfirmPassword, string DisplayName, string Nickname);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest(string? RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);
public sealed record VerifyEmailRequest(string Code);
public sealed record JwtAccessTokenDto(string Token, DateTime ExpiresAt);
public sealed record AuthResultDto(
    int UserId,
    string Email,
    string Role,
    string Token,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    string TokenType = "Bearer");
public sealed record CurrentUserDto(
    int UserId,
    string Email,
    string Role,
    bool EmailConfirmed,
    string DisplayName,
    string Nickname,
    string ProfileImageUrl,
    bool CanSubscribeToEmail);

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
    DateTime? VerificationRetryAfter);

public sealed record UpdateProfileRequest(string? DisplayName, string? Nickname, string? ProfileImageUrl);
public sealed record DeleteAccountRequest(string ConfirmText, string Password);

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

public sealed record CreateCommentRequest(int StationId, int? FuelId, string Content, int? Rating = null);
public sealed record UpdateCommentRequest(string Content, int? Rating = null);
public sealed record RealtimePresenceDto(int TotalConnections, int AnonymousConnections, int AuthenticatedUsers);

public sealed record SubscriptionRequest(int FuelId, string City, SubscriptionFrequency Frequency);
public sealed record SubscriptionDto(int Id, int FuelId, string FuelCode, string FuelName, string City, SubscriptionFrequency Frequency, DateTime CreatedAt);

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
public sealed record ApiTokenDto(int Id, string Name, string Scopes, DateTime CreatedAt, DateTime? ExpiresAt, DateTime? RevokedAt, int CreatedByUserId, string CreatedByEmail);

public sealed record UserAdminDto(int Id, string Email, string Role, bool EmailConfirmed, DateTime CreatedAt, DateTime? LastLoginAt, string DisplayName, string Nickname);
public sealed record UpdateUserRoleRequest(UserRole Role);

public sealed record DataSourceDto(int Id, string Name, string Url, DataSourceType Type, bool Enabled, DateTime? LastSuccessAt);
public sealed record UpsertDataSourceRequest(string Name, string Url, DataSourceType Type, bool Enabled);
public sealed record ParserRunDto(int Id, int SourceId, string SourceName, DateTime StartedAt, DateTime? FinishedAt, string Status, int RecordsFound, int RecordsSaved, string? Error);

public sealed record FuelPriceRecord(string StationName, string City, string FuelName, decimal Price, DateTime Date, string? Address = null, decimal? Latitude = null, decimal? Longitude = null);
public sealed record FuelImportResult(int RecordsFound, int RecordsSaved, IReadOnlyList<PriceChangeNotificationDto>? PriceChanges = null);

public sealed record ApiUsageMetricDto(string Controller, long Count, double AverageMilliseconds);

public sealed record ChatRequest(string Message, string? SessionId = null, string? City = null, string? FuelCode = null, int? StationId = null);
public sealed record ChatResponseDto(string Answer, string SessionId, string Intent, string Status, DateTime CreatedAt);
public sealed record ChatHistoryDto(int Id, string SessionId, int? UserId, string Message, string Answer, string Intent, string Status, DateTime CreatedAt);

public sealed record ChatTopicDecision(bool IsAllowed, string Intent, bool IsSecurityBlocked = false);
public sealed record ChatContextResult(bool HasRequiredData, bool RequiresFuelData, string Intent, string Context, bool UsesExternalContext = false, string? DirectAnswer = null);
