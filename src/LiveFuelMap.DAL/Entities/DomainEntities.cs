using LiveFuelMap.DAL.Enums;

namespace LiveFuelMap.DAL.Entities;

public sealed class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Nickname { get; set; }
    public string? NormalizedNickname { get; set; }
    public string? ProfileImageUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool EmailConfirmed { get; set; }
    public string? EmailConfirmationTokenHash { get; set; }
    public DateTime? EmailConfirmationTokenExpiresAt { get; set; }
    public DateTime? LastEmailConfirmationSentAt { get; set; }
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }
    public string? RefreshTokenHash { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }
    public DateTime? RefreshTokenRevokedAt { get; set; }
    public int TokenVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public ICollection<Subscription> Subscriptions { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<ApiToken> CreatedApiTokens { get; set; } = [];
    public ICollection<ChatMessage> ChatMessages { get; set; } = [];
}

public sealed class Station
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedKey { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? ImageUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? PhotoUrls { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<FuelPrice> FuelPrices { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
}

public sealed class Fuel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ICollection<FuelPrice> FuelPrices { get; set; } = [];
    public ICollection<Subscription> Subscriptions { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
}

public sealed class FuelPrice
{
    public int Id { get; set; }
    public int StationId { get; set; }
    public int FuelId { get; set; }
    public decimal Price { get; set; }
    public int Popularity { get; set; }
    public DateTime Date { get; set; }
    public int? SourceId { get; set; }
    public bool IsManual { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Station Station { get; set; } = null!;
    public Fuel Fuel { get; set; } = null!;
    public DataSource? Source { get; set; }
}

public sealed class Subscription
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int FuelId { get; set; }
    public string City { get; set; } = string.Empty;
    public SubscriptionFrequency Frequency { get; set; } = SubscriptionFrequency.Daily;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Fuel Fuel { get; set; } = null!;
}

public sealed class Comment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int StationId { get; set; }
    public int? FuelId { get; set; }
    public int Rating { get; set; } = 5;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Station Station { get; set; } = null!;
    public Fuel? Fuel { get; set; }
}

public sealed class ApiToken
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int CreatedByUserId { get; set; }

    public User CreatedByUser { get; set; } = null!;
}

public sealed class ChatMessage
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
    public string BotResponse { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? City { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}

public sealed class DataSource
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DataSourceType Type { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime? LastSuccessAt { get; set; }

    public ICollection<FuelPrice> FuelPrices { get; set; } = [];
    public ICollection<ParserRun> ParserRuns { get; set; } = [];
}

public sealed class ParserRun
{
    public int Id { get; set; }
    public int SourceId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public ParserRunStatus Status { get; set; } = ParserRunStatus.Running;
    public int RecordsFound { get; set; }
    public int RecordsSaved { get; set; }
    public string? Error { get; set; }
    public TimeSpan? Duration { get; set; }

    public DataSource Source { get; set; } = null!;
}

public sealed class ApplicationLog
{
    public int Id { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
