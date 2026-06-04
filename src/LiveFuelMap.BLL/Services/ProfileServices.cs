using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.DAL.Entities;
using LiveFuelMap.DAL.Enums;
using LiveFuelMap.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace LiveFuelMap.BLL.Services;

public sealed class ProfileService(
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenHasher tokenHasher,
    IEmailSender emailSender,
    ILogger<ProfileService> logger) : IProfileService
{
    private const string GoogleAuthProvider = "Google";
    private const string DeletedAuthProvider = "Deleted";
    private static readonly TimeSpan AccountDeletionCodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AccountDeletionResendDelay = TimeSpan.FromMinutes(1);

    public async Task<ProfileDto> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (user.IsDeleted)
            throw new InvalidOperationException("User not found.");

        return ToProfileDto(user);
    }

    public async Task<ProfileDto> SetupNicknameAsync(int userId, SetupNicknameRequest request, CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (user.IsDeleted)
            throw new InvalidOperationException("User not found.");

        if (!user.RequiresNicknameSetup && !string.IsNullOrWhiteSpace(user.Nickname))
            throw new InvalidOperationException("Nickname is already configured.");

        var nickname = NormalizeNicknameInput(request.Nickname);
        var normalizedNickname = NormalizeNickname(nickname);

        if (!IsValidNickname(nickname))
            throw new InvalidOperationException("Нікнейм має містити 3-24 символи: літери, цифри, дефіс або underscore.");

        var exists = await unitOfWork.Users.ExistsAsync(
            x => !x.IsDeleted && x.Id != user.Id && x.NormalizedNickname == normalizedNickname,
            cancellationToken);

        if (exists)
            throw new InvalidOperationException("Цей нікнейм уже використовується.");

        user.Nickname = nickname;
        user.NormalizedNickname = normalizedNickname;
        user.RequiresNicknameSetup = false;

        if (string.IsNullOrWhiteSpace(user.DisplayName))
            user.DisplayName = nickname;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToProfileDto(user);
    }

    public async Task<ProfileDto> UpdateAsync(int userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (user.IsDeleted)
            throw new InvalidOperationException("User not found.");

        if (user.RequiresNicknameSetup)
            throw new InvalidOperationException("Complete nickname setup first.");

        if (request.DisplayName is not null)
        {
            var displayName = request.DisplayName.Trim();
            if (displayName.Length > 100)
                throw new InvalidOperationException("Display name is too long.");
            if (!string.IsNullOrWhiteSpace(displayName) && displayName.Length < 2)
                throw new InvalidOperationException("Display name must contain at least 2 characters.");

            user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName;
        }

        if (request.Nickname is not null)
        {
            var nickname = NormalizeNicknameInput(request.Nickname);
            var normalizedNickname = NormalizeNickname(nickname);

            if (!IsValidNickname(nickname))
                throw new InvalidOperationException("Nickname must contain 3-24 letters, numbers, underscores or hyphens.");

            var exists = await unitOfWork.Users.ExistsAsync(
                x => !x.IsDeleted && x.Id != user.Id && x.NormalizedNickname == normalizedNickname,
                cancellationToken);

            if (exists)
                throw new InvalidOperationException("Nickname is already used.");

            user.Nickname = nickname;
            user.NormalizedNickname = normalizedNickname;
        }

        if (request.ProfileImageUrl is not null)
        {
            var imageUrl = request.ProfileImageUrl.Trim();
            if (imageUrl.Length > 500)
                throw new InvalidOperationException("Profile image URL is too long.");

            if (!string.IsNullOrWhiteSpace(imageUrl) && !IsAllowedProfileImageUrl(imageUrl))
                throw new InvalidOperationException("Profile image must be an HTTP(S) URL or an uploaded profile image.");

            user.ProfileImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToProfileDto(user);
    }

    public async Task RequestDeletionCodeAsync(
        int userId,
        DeleteAccountVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (user.IsDeleted)
            throw new InvalidOperationException("User not found.");

        if (!IsGoogleAccount(user))
            throw new InvalidOperationException("Password confirmation is required for this account.");

        if (!MatchesEmail(user, request.Email))
            throw new InvalidOperationException("Email does not match the current account.");

        var now = DateTime.UtcNow;
        if (user.LastAccountDeletionTokenSentAt is not null &&
            user.LastAccountDeletionTokenSentAt.Value.Add(AccountDeletionResendDelay) > now)
        {
            var retryAt = user.LastAccountDeletionTokenSentAt.Value.Add(AccountDeletionResendDelay);
            throw new InvalidOperationException($"Please wait until {retryAt:yyyy-MM-dd HH:mm:ss} UTC before requesting a new deletion code.");
        }

        var code = CreateVerificationCode();
        user.AccountDeletionTokenHash = tokenHasher.Hash(code);
        user.AccountDeletionTokenExpiresAt = now.Add(AccountDeletionCodeLifetime);
        user.LastAccountDeletionTokenSentAt = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailSender.SendEmailAsync(
            user.Email,
            "LiveFuelMap: account deletion code",
            BuildAccountDeletionEmail(code, user.AccountDeletionTokenExpiresAt.Value),
            isHtml: true,
            cancellationToken: cancellationToken);

        logger.LogInformation("Account deletion code sent for user {UserId}", user.Id);
    }

    public async Task DeleteAsync(int userId, DeleteAccountRequest request, CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (user.IsDeleted)
            throw new InvalidOperationException("User not found.");

        if (IsGoogleAccount(user))
        {
            if (!MatchesEmail(user, request.Email))
                throw new InvalidOperationException("Email does not match the current account.");

            if (string.IsNullOrWhiteSpace(request.VerificationCode) ||
                string.IsNullOrWhiteSpace(user.AccountDeletionTokenHash) ||
                user.AccountDeletionTokenExpiresAt is null ||
                user.AccountDeletionTokenExpiresAt <= DateTime.UtcNow ||
                !tokenHasher.Verify(request.VerificationCode.Trim(), user.AccountDeletionTokenHash))
            {
                throw new InvalidOperationException("Deletion confirmation code is invalid or expired.");
            }
        }
        else
        {
            if (!MatchesLocalIdentifier(user, request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                !passwordHasher.Verify(request.Password, user.PasswordHash))
            {
                throw new InvalidOperationException("Email/login or password is invalid.");
            }
        }

        var apiTokens = await unitOfWork.ApiTokens.Query()
            .Where(x => x.CreatedByUserId == user.Id)
            .ToListAsync(cancellationToken);

        foreach (var token in apiTokens)
            token.RevokedAt ??= DateTime.UtcNow;

        var subscriptions = await unitOfWork.Subscriptions.Query()
            .Where(x => x.UserId == user.Id)
            .ToListAsync(cancellationToken);

        foreach (var subscription in subscriptions)
            unitOfWork.Subscriptions.Remove(subscription);

        var chatMessages = await unitOfWork.ChatMessages.Query()
            .Where(x => x.UserId == user.Id)
            .ToListAsync(cancellationToken);

        foreach (var message in chatMessages)
            message.UserId = null;

        SoftDeleteUser(user);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("User account soft-deleted: {UserId}", user.Id);
    }

    private static ProfileDto ToProfileDto(User user)
    {
        DateTime? retryAfter = user.EmailConfirmed || user.LastEmailConfirmationSentAt is null
            ? null
            : user.LastEmailConfirmationSentAt.Value.AddMinutes(1);

        return new ProfileDto(
            user.Id,
            user.Email,
            user.Role.ToString(),
            GetDisplayName(user),
            GetNickname(user),
            GetProfileImageUrl(user),
            user.EmailConfirmed,
            user.CreatedAt,
            user.LastLoginAt,
            user.EmailConfirmationTokenExpiresAt,
            retryAfter,
            user.AuthProvider,
            user.RequiresNicknameSetup);
    }

    private void SoftDeleteUser(User user)
    {
        var now = DateTime.UtcNow;
        var deletedSlug = $"deleted-user-{user.Id}";

        user.IsDeleted = true;
        user.DeletedAt = now;
        user.Email = $"{deletedSlug}@deleted.livefuelmap.local";
        user.PasswordHash = passwordHasher.Hash(CreateSecret());
        user.DisplayName = "Deleted user";
        user.Nickname = deletedSlug;
        user.NormalizedNickname = NormalizeNickname(deletedSlug);
        user.ProfileImageUrl = null;
        user.AuthProvider = DeletedAuthProvider;
        user.ExternalProviderId = null;
        user.GoogleName = null;
        user.RequiresNicknameSetup = false;
        user.EmailConfirmed = false;
        user.EmailConfirmationTokenHash = null;
        user.EmailConfirmationTokenExpiresAt = null;
        user.LastEmailConfirmationSentAt = null;
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;
        user.AccountDeletionTokenHash = null;
        user.AccountDeletionTokenExpiresAt = null;
        user.LastAccountDeletionTokenSentAt = null;
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAt = null;
        user.RefreshTokenRevokedAt = now;
        user.TokenVersion++;
    }

    private static bool IsGoogleAccount(User user) =>
        string.Equals(user.AuthProvider, GoogleAuthProvider, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesEmail(User user, string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        string.Equals(user.Email, NormalizeEmail(value), StringComparison.OrdinalIgnoreCase);

    private static bool MatchesLocalIdentifier(User user, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalizedValue = value.Trim();
        if (string.Equals(user.Email, NormalizeEmail(normalizedValue), StringComparison.OrdinalIgnoreCase))
            return true;

        return !string.IsNullOrWhiteSpace(user.NormalizedNickname) &&
            string.Equals(user.NormalizedNickname, NormalizeNickname(NormalizeNicknameInput(normalizedValue)), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeEmail(string value) =>
        value.Trim().ToLowerInvariant();

    private static string CreateSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string CreateVerificationCode() =>
        RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    private static string BuildAccountDeletionEmail(string code, DateTime expiresAt) =>
        $$"""
        <!doctype html>
        <html lang="uk">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>LiveFuelMap account deletion</title>
        </head>
        <body style="margin:0;padding:0;background:#edf4f6;font-family:'Segoe UI',Arial,sans-serif;color:#20323a;">
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#edf4f6;padding:32px 12px;">
                <tr>
                    <td align="center">
                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:540px;background:#ffffff;border:1px solid #d9e1e4;border-radius:18px;overflow:hidden;">
                            <tr>
                                <td style="background:#7f1d1d;padding:24px 28px;text-align:center;color:#ffffff;">
                                    <div style="font-size:15px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;">LiveFuelMap</div>
                                    <div style="margin-top:8px;font-size:24px;line-height:1.25;font-weight:800;">Account deletion confirmation</div>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding:32px 28px;text-align:center;">
                                    <div style="font-size:16px;line-height:1.5;color:#20323a;">Enter this code in your profile to delete the account.</div>
                                    <div style="margin:22px auto 24px;padding:20px 28px;display:inline-block;min-width:240px;border-radius:16px;background:#fff7ed;border:2px solid #dc2626;color:#7f1d1d;font-size:42px;line-height:1;font-weight:900;letter-spacing:.18em;text-align:center;">
                                        {{code}}
                                    </div>
                                    <div style="font-size:14px;line-height:1.5;color:#637176;">
                                        The code is valid until <strong style="color:#20323a;">{{expiresAt:yyyy-MM-dd HH:mm:ss}} UTC</strong>. Ignore this email if you did not request account deletion.
                                    </div>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>
        """;

    private static string GetDisplayName(User user)
    {
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
            return user.DisplayName.Trim();

        var local = user.Email.Split('@')[0];
        return string.IsNullOrWhiteSpace(local) ? "User" : local;
    }

    private static string GetProfileImageUrl(User user) =>
        string.IsNullOrWhiteSpace(user.ProfileImageUrl) ? "default-station.jpg" : user.ProfileImageUrl.Trim();

    private static string GetNickname(User user)
    {
        if (!string.IsNullOrWhiteSpace(user.Nickname))
            return user.Nickname.Trim();

        return string.Empty;
    }

    private static string NormalizeNicknameInput(string nickname) =>
        nickname.Trim();

    private static string NormalizeNickname(string nickname) =>
        nickname.Trim().ToLowerInvariant();

    private static bool IsValidNickname(string nickname) =>
        Regex.IsMatch(nickname, @"^[\p{L}\p{Nd}_-]{3,24}$");

    private static bool IsAllowedProfileImageUrl(string imageUrl)
    {
        if (imageUrl.StartsWith("/uploads/profiles/", StringComparison.OrdinalIgnoreCase))
            return true;

        return Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

public sealed class PriceChangeEmailNotifier(
    IUnitOfWork unitOfWork,
    IEmailSender emailSender,
    ILogger<PriceChangeEmailNotifier> logger) : IPriceChangeEmailNotifier, ISubscriptionEmailNotifier
{
    private const string KharkivDisplayName = "Харків";
    private const string BorderColor = "#d9e1e4";
    private const string PrimaryColor = "#20323a";
    private const string AccentColor = "#0f8b8d";

    private sealed record FuelPriceCell(decimal? Before, decimal? After, decimal? Delta, decimal? Percent, DateTime? BeforeDate, DateTime? AfterDate);
    private sealed record FuelPriceTableRow(string Operator, IReadOnlyDictionary<int, FuelPriceCell> Cells);
    private sealed record FuelPriceTable(string City, IReadOnlyList<Fuel> Fuels, IReadOnlyList<FuelPriceTableRow> Rows);

    public async Task NotifySubscriptionCreatedAsync(int userId, int subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await unitOfWork.Subscriptions.Query()
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Fuel)
            .FirstOrDefaultAsync(x => x.Id == subscriptionId && x.UserId == userId, cancellationToken);

        if (subscription is null || !subscription.User.EmailConfirmed)
            return;

        var email = SubscriptionEmail(subscription);
        await emailSender.SendEmailAsync(
            email,
            "LiveFuelMap: підписку оформлено",
            BuildSubscriptionConfirmationEmail(subscription),
            isHtml: true,
            cancellationToken: cancellationToken);
    }

    public async Task NotifyAsync(IReadOnlyList<PriceChangeNotificationDto> priceChanges, CancellationToken cancellationToken = default)
    {
        if (priceChanges.Count == 0)
            return;

        var affectedFuelIds = priceChanges.Select(x => x.FuelId).Distinct().ToList();
        var affectedCities = priceChanges.Select(x => NormalizeCity(x.StationCity)).Distinct().ToList();

        var subscriptions = await unitOfWork.Subscriptions.Query()
            .Include(x => x.User)
            .Include(x => x.Fuel)
            .Where(x =>
                x.IsActive &&
                x.Frequency == SubscriptionFrequency.Immediate &&
                affectedFuelIds.Contains(x.FuelId) &&
                affectedCities.Contains(x.City) &&
                x.User.EmailConfirmed &&
                !x.User.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var userCityGroup in subscriptions.GroupBy(x => new { x.UserId, Email = SubscriptionEmail(x), x.City }))
        {
            var dueSubscriptions = userCityGroup
                .Where(subscription => priceChanges.Any(change =>
                    NormalizeCity(change.StationCity) == userCityGroup.Key.City &&
                    change.FuelId == subscription.FuelId &&
                    ShouldSendImmediate(subscription, change)))
                .ToList();

            if (dueSubscriptions.Count == 0)
                continue;

            var selectedFuelIds = dueSubscriptions.Select(x => x.FuelId).Distinct().ToList();
            var cityChanges = priceChanges
                .Where(x => NormalizeCity(x.StationCity) == userCityGroup.Key.City && selectedFuelIds.Contains(x.FuelId))
                .ToList();

            if (cityChanges.Count == 0)
                continue;

            var table = await BuildFuelPriceTableAsync(userCityGroup.Key.City, selectedFuelIds, cityChanges, cancellationToken);

            try
            {
                await emailSender.SendEmailAsync(
                    userCityGroup.Key.Email,
                    $"LiveFuelMap: зміни цін у місті {DisplayCity(userCityGroup.Key.City)}",
                    BuildFuelDigestEmail(
                        "Зміни цін за вашою підпискою",
                        "Оновлена таблиця показує ціну до зміни, ціну після зміни та відсоток зміни для вибраних типів палива.",
                        table),
                    isHtml: true,
                    cancellationToken: cancellationToken);

                var now = DateTime.UtcNow;
                foreach (var subscription in dueSubscriptions)
                    subscription.LastSentAt = now;

                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send price change email to {Email}", userCityGroup.Key.Email);
            }
        }
    }

    public async Task NotifyDueScheduledAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var localNow = DateTime.Now;
        var localToday = localNow.Date;
        var currentTime = new TimeSpan(localNow.Hour, localNow.Minute, 0);

        var subscriptions = await unitOfWork.Subscriptions.Query()
            .Include(x => x.User)
            .Include(x => x.Fuel)
            .Where(x =>
                x.IsActive &&
                (x.Frequency == SubscriptionFrequency.Daily || x.Frequency == SubscriptionFrequency.Weekly) &&
                x.SendTime <= currentTime &&
                x.User.EmailConfirmed &&
                !x.User.IsDeleted)
            .ToListAsync(cancellationToken);

        var dueSubscriptions = subscriptions
            .Where(x => IsScheduledDue(x, localToday, utcNow))
            .ToList();

        foreach (var group in dueSubscriptions.GroupBy(x => new { x.UserId, Email = SubscriptionEmail(x), x.City, x.Frequency, x.SendTime }))
        {
            var selectedFuelIds = group.Select(x => x.FuelId).Distinct().ToList();
            if (selectedFuelIds.Count == 0)
                continue;

            var table = await BuildFuelPriceTableAsync(group.Key.City, selectedFuelIds, [], cancellationToken);
            var isWeekly = group.Key.Frequency == SubscriptionFrequency.Weekly;

            try
            {
                await emailSender.SendEmailAsync(
                    group.Key.Email,
                    isWeekly
                        ? $"LiveFuelMap: щотижневий звіт цін у місті {DisplayCity(group.Key.City)}"
                        : $"LiveFuelMap: щоденний звіт цін у місті {DisplayCity(group.Key.City)}",
                    BuildFuelDigestEmail(
                        isWeekly ? "Щотижневий звіт за підпискою" : "Щоденний звіт за підпискою",
                        isWeekly
                            ? "Нижче зібрана таблиця актуальних і попередніх цін для вибраних типів пального за вашим щотижневим графіком."
                            : "Нижче зібрана таблиця актуальних і попередніх цін для вибраних типів пального за вашим щоденним графіком.",
                        table),
                    isHtml: true,
                    cancellationToken: cancellationToken);

                foreach (var subscription in group)
                    subscription.LastSentAt = utcNow;

                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send scheduled subscription email to {Email}", group.Key.Email);
            }
        }
    }

    private async Task<IReadOnlyList<int>> GetUserSubscribedFuelIdsAsync(int userId, string city, CancellationToken cancellationToken)
    {
        var normalizedCity = NormalizeCity(city);
        return await unitOfWork.Subscriptions.Query()
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.City == normalizedCity && x.IsActive)
            .Select(x => x.FuelId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task<FuelPriceTable> BuildFuelPriceTableAsync(
        string city,
        IReadOnlyList<int> selectedFuelIds,
        IReadOnlyList<PriceChangeNotificationDto> priceChanges,
        CancellationToken cancellationToken)
    {
        var normalizedCity = NormalizeCity(city);
        var fuels = await unitOfWork.Fuels.Query()
            .AsNoTracking()
            .Where(x => selectedFuelIds.Contains(x.Id))
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        var stations = (await unitOfWork.Stations.Query()
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken))
            .Where(x => NormalizeCity(x.City) == normalizedCity)
            .ToList();

        var stationIds = stations.Select(x => x.Id).ToList();
        var fuelIds = fuels.Select(x => x.Id).ToList();
        var prices = await unitOfWork.FuelPrices.Query()
            .AsNoTracking()
            .Where(x => stationIds.Contains(x.StationId) && fuelIds.Contains(x.FuelId))
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var history = prices
            .GroupBy(x => (x.StationId, x.FuelId))
            .ToDictionary(x => x.Key, x => x.Take(2).ToList());

        var changes = priceChanges
            .GroupBy(x => (x.StationId, x.FuelId))
            .ToDictionary(x => x.Key, x => x.Last());

        var rows = stations.Select(station =>
        {
            var cells = fuels.ToDictionary(
                fuel => fuel.Id,
                fuel =>
                {
                    decimal? before = null;
                    decimal? after = null;
                    DateTime? beforeDate = null;
                    DateTime? afterDate = null;

                    if (changes.TryGetValue((station.Id, fuel.Id), out var change))
                    {
                        before = change.OldPrice;
                        after = change.NewPrice;
                        afterDate = change.Date;

                        if (history.TryGetValue((station.Id, fuel.Id), out var changedHistory))
                        {
                            var previous = changedHistory.FirstOrDefault(x => x.Price == change.OldPrice)
                                ?? changedHistory.ElementAtOrDefault(1);
                            beforeDate = previous?.Date;
                        }
                    }
                    else if (history.TryGetValue((station.Id, fuel.Id), out var stationFuelPrices))
                    {
                        var latest = stationFuelPrices.ElementAtOrDefault(0);
                        var previous = stationFuelPrices.ElementAtOrDefault(1);
                        after = latest?.Price;
                        afterDate = latest?.Date;
                        before = previous?.Price;
                        beforeDate = previous?.Date;
                    }

                    decimal? delta = after is null || before is null ? null : after.Value - before.Value;
                    var percent = CalculatePercent(before, after);
                    return new FuelPriceCell(before, after, delta, percent, beforeDate, afterDate);
                });

            return new FuelPriceTableRow(station.Name, cells);
        }).ToList();

        return new FuelPriceTable(DisplayCity(normalizedCity), fuels, rows);
    }

    private static string BuildSubscriptionConfirmationEmail(Subscription subscription)
    {
        var fuelName = DisplayFuelName(subscription.Fuel);
        var city = DisplayCity(subscription.City);
        var email = SubscriptionEmail(subscription);
        return BuildEmailShell(
            "Підписку оформлено",
            $"""
            <p style="margin:0 0 18px;font-size:16px;line-height:1.6;color:#52666d;">
                Дякуємо за оформлення підписки на розсилку повідомлень LiveFuelMap. Оновлення надходитимуть згідно з обраним графіком.
            </p>
            <div style="background:#f5fbfb;border:1px solid #cbe4e5;border-radius:14px;padding:18px;text-align:left;">
                <div style="font-size:14px;color:#637176;">Місто</div>
                <div style="font-size:18px;font-weight:700;color:{PrimaryColor};margin-bottom:12px;">{WebUtility.HtmlEncode(city)}</div>
                <div style="font-size:14px;color:#637176;">Тип палива</div>
                <div style="font-size:18px;font-weight:700;color:{AccentColor};margin-bottom:12px;">{WebUtility.HtmlEncode(fuelName)}</div>
                <div style="font-size:14px;color:#637176;">Графік</div>
                <div style="font-size:18px;font-weight:700;color:{PrimaryColor};margin-bottom:12px;">{WebUtility.HtmlEncode(DisplayFrequency(subscription.Frequency))} · {FormatSendTime(subscription.SendTime)}</div>
                <div style="font-size:14px;color:#637176;">Email</div>
                <div style="font-size:18px;font-weight:700;color:{PrimaryColor};">{WebUtility.HtmlEncode(email)}</div>
            </div>
            """);
    }

    private static string BuildFuelDigestEmail(string title, string intro, FuelPriceTable table)
    {
        var selectedFuels = table.Fuels.Count == 0
            ? "немає вибраних типів палива"
            : string.Join(", ", table.Fuels.Select(DisplayFuelName));

        return BuildEmailShell(
            title,
            $"""
            <p style="margin:0 0 12px;font-size:16px;line-height:1.6;color:#52666d;">{WebUtility.HtmlEncode(intro)}</p>
            <p style="margin:0 0 20px;font-size:14px;line-height:1.6;color:#637176;">
                Місто: <strong style="color:{PrimaryColor};">{WebUtility.HtmlEncode(table.City)}</strong><br>
                Вибрані типи палива: <strong style="color:{PrimaryColor};">{WebUtility.HtmlEncode(selectedFuels)}</strong>
            </p>
            {BuildPriceTableHtml(table)}
            """);
    }

    private static string BuildPriceTableHtml(FuelPriceTable table)
    {
        if (table.Fuels.Count == 0)
            return """<div style="padding:18px;border:1px solid #e2e9eb;border-radius:12px;color:#637176;">Немає вибраних типів палива.</div>""";

        var fuelHeaders = string.Concat(table.Fuels.Select(fuel =>
            $"""<th colspan="6" style="padding:12px 10px;background:{PrimaryColor};color:#ffffff;border:1px solid {PrimaryColor};font-size:13px;text-align:center;">{WebUtility.HtmlEncode(DisplayFuelName(fuel))}</th>"""));

        var subHeaders = string.Concat(table.Fuels.Select(_ =>
            $"""<th style="padding:9px 8px;background:#eef6f7;color:{PrimaryColor};border:1px solid {BorderColor};font-size:12px;text-align:right;">Стара</th><th style="padding:9px 8px;background:#eef6f7;color:{PrimaryColor};border:1px solid {BorderColor};font-size:12px;text-align:center;">Дата</th><th style="padding:9px 8px;background:#eef6f7;color:{PrimaryColor};border:1px solid {BorderColor};font-size:12px;text-align:right;">Нова</th><th style="padding:9px 8px;background:#eef6f7;color:{PrimaryColor};border:1px solid {BorderColor};font-size:12px;text-align:center;">Дата</th><th style="padding:9px 8px;background:#eef6f7;color:{PrimaryColor};border:1px solid {BorderColor};font-size:12px;text-align:right;">Δ грн</th><th style="padding:9px 8px;background:#eef6f7;color:{PrimaryColor};border:1px solid {BorderColor};font-size:12px;text-align:right;">%</th>"""));

        var rows = table.Rows.Count == 0
            ? $"""<tr><td colspan="{1 + table.Fuels.Count * 6}" style="padding:18px;border:1px solid {BorderColor};text-align:center;color:#637176;">Немає АЗС для вибраного міста.</td></tr>"""
            : string.Concat(table.Rows.Select((row, index) =>
            {
                var background = index % 2 == 0 ? "#ffffff" : "#f8fbfc";
                var cells = string.Concat(table.Fuels.Select(fuel =>
                {
                    var cell = row.Cells[fuel.Id];
                    return $"""
                        <td style="padding:10px 8px;background:{background};border:1px solid {BorderColor};font-size:13px;text-align:right;white-space:nowrap;">{FormatPrice(cell.Before)}</td>
                        <td style="padding:10px 8px;background:{background};border:1px solid {BorderColor};font-size:12px;text-align:center;white-space:nowrap;color:#637176;">{FormatDate(cell.BeforeDate)}</td>
                        <td style="padding:10px 8px;background:{background};border:1px solid {BorderColor};font-size:13px;text-align:right;white-space:nowrap;font-weight:700;color:{PrimaryColor};">{FormatPrice(cell.After)}</td>
                        <td style="padding:10px 8px;background:{background};border:1px solid {BorderColor};font-size:12px;text-align:center;white-space:nowrap;color:#637176;">{FormatDate(cell.AfterDate)}</td>
                        <td style="padding:10px 8px;background:{background};border:1px solid {BorderColor};font-size:13px;text-align:right;white-space:nowrap;font-weight:700;color:{PercentColor(cell.Delta)};">{FormatDelta(cell.Delta)}</td>
                        <td style="padding:10px 8px;background:{background};border:1px solid {BorderColor};font-size:13px;text-align:right;white-space:nowrap;font-weight:700;color:{PercentColor(cell.Percent)};">{FormatPercent(cell.Percent)}</td>
                        """;
                }));

                return $"""
                    <tr>
                        <td style="padding:10px 12px;background:{background};border:1px solid {BorderColor};font-size:13px;font-weight:700;color:{PrimaryColor};white-space:nowrap;">{WebUtility.HtmlEncode(row.Operator)}</td>
                        {cells}
                    </tr>
                    """;
            }));

        return $"""
            <div style="width:100%;overflow-x:auto;">
                <table role="table" cellspacing="0" cellpadding="0" style="width:100%;min-width:{Math.Max(760, 180 + table.Fuels.Count * 430)}px;border-collapse:collapse;border:1px solid {BorderColor};border-radius:12px;overflow:hidden;">
                    <thead>
                        <tr>
                            <th rowspan="2" style="padding:12px;background:{PrimaryColor};color:#ffffff;border:1px solid {PrimaryColor};font-size:13px;text-align:left;vertical-align:middle;">Оператор</th>
                            {fuelHeaders}
                        </tr>
                        <tr>{subHeaders}</tr>
                    </thead>
                    <tbody>{rows}</tbody>
                </table>
            </div>
            """;
    }

    private static string BuildEmailShell(string title, string content) =>
        $$"""
        <!doctype html>
        <html lang="uk">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>LiveFuelMap</title>
        </head>
        <body style="margin:0;padding:0;background:#edf4f6;font-family:'Segoe UI',Arial,sans-serif;color:{{PrimaryColor}};">
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#edf4f6;padding:32px 12px;">
                <tr>
                    <td align="center">
                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:860px;background:#ffffff;border:1px solid {{BorderColor}};border-radius:18px;overflow:hidden;box-shadow:0 18px 38px rgba(25,39,45,.16);">
                            <tr>
                                <td style="background:{{PrimaryColor}};padding:24px 28px;text-align:center;">
                                    <div style="font-size:15px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;color:#bde7e7;">LiveFuelMap</div>
                                    <div style="margin-top:8px;font-size:24px;line-height:1.25;font-weight:700;color:#ffffff;">{{WebUtility.HtmlEncode(title)}}</div>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding:30px;">
                                    {{content}}
                                </td>
                            </tr>
                            <tr>
                                <td style="padding:0 30px 28px;text-align:center;">
                                    <div style="border-top:1px solid #edf1f2;padding-top:18px;font-size:13px;line-height:1.5;color:#7a888d;">
                                        Ви отримали цей лист, тому що оформили підписку в LiveFuelMap.
                                    </div>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>
        """;

    private static bool ShouldSendImmediate(Subscription subscription, PriceChangeNotificationDto change) =>
        subscription.IsActive && subscription.Frequency == SubscriptionFrequency.Immediate;

    private static bool IsScheduledDue(Subscription subscription, DateTime localToday, DateTime utcNow)
    {
        if (subscription.LastSentAt is null)
            return true;

        var lastLocalDate = subscription.LastSentAt.Value.ToLocalTime().Date;
        return subscription.Frequency switch
        {
            SubscriptionFrequency.Daily => lastLocalDate < localToday,
            SubscriptionFrequency.Weekly => subscription.LastSentAt.Value <= utcNow.AddDays(-7),
            _ => false
        };
    }

    private static string SubscriptionEmail(Subscription subscription) =>
        string.IsNullOrWhiteSpace(subscription.Email) ? subscription.User.Email : subscription.Email.Trim();

    private static string DisplayFrequency(SubscriptionFrequency frequency) =>
        frequency switch
        {
            SubscriptionFrequency.Immediate => "Відразу після оновлення",
            SubscriptionFrequency.Daily => "Кожного дня",
            SubscriptionFrequency.Weekly => "Кожного тижня",
            _ => frequency.ToString()
        };

    private static string FormatSendTime(TimeSpan value) =>
        $"{value.Hours:00}:{value.Minutes:00}";

    private static decimal? CalculatePercent(decimal? before, decimal? after)
    {
        if (before is null || after is null || before.Value == 0)
            return null;

        return Math.Round((after.Value - before.Value) / before.Value * 100m, 2);
    }

    private static string FormatPrice(decimal? price) =>
        price is null ? "—" : price.Value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatDelta(decimal? delta)
    {
        if (delta is null)
            return "—";

        var sign = delta.Value > 0 ? "+" : string.Empty;
        return $"{sign}{delta.Value.ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    private static string FormatDate(DateTime? value) =>
        value is null ? "—" : value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatPercent(decimal? percent)
    {
        if (percent is null)
            return "—";

        var sign = percent.Value > 0 ? "+" : string.Empty;
        return $"{sign}{percent.Value.ToString("0.##", CultureInfo.InvariantCulture)}%";
    }

    private static string PercentColor(decimal? percent)
    {
        if (percent is null || percent.Value == 0)
            return "#637176";

        return percent.Value > 0 ? "#c2413b" : "#138a4f";
    }

    private static string DisplayFuelName(Fuel fuel) =>
        fuel.Code switch
        {
            "a95plus" => "А-95+",
            "a95" => "А-95",
            "a92" => "А-92",
            "diesel" => "ДТ",
            "gas" => "Газ",
            _ => fuel.Name
        };

    private static string DisplayCity(string city) =>
        NormalizeCity(city) == "харків" ? KharkivDisplayName : city.Trim();

    private static string NormalizeCity(string city) =>
        city.Trim().ToLowerInvariant();
}
