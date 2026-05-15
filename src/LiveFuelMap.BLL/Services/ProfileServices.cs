using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.DAL.Entities;
using LiveFuelMap.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace LiveFuelMap.BLL.Services;

public sealed class ProfileService(
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ILogger<ProfileService> logger) : IProfileService
{
    private const string DeleteConfirmationText = "DELETE";

    public async Task<ProfileDto> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        return ToProfileDto(user);
    }

    public async Task<ProfileDto> UpdateAsync(int userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

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
                x => x.Id != user.Id && x.NormalizedNickname == normalizedNickname,
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

    public async Task DeleteAsync(int userId, DeleteAccountRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.ConfirmText, DeleteConfirmationText, StringComparison.Ordinal))
            throw new InvalidOperationException("Deletion confirmation is invalid.");

        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (string.IsNullOrWhiteSpace(request.Password) || !passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new InvalidOperationException("Password is invalid.");

        var apiTokens = await unitOfWork.ApiTokens.Query()
            .Where(x => x.CreatedByUserId == user.Id)
            .ToListAsync(cancellationToken);

        foreach (var token in apiTokens)
            unitOfWork.ApiTokens.Remove(token);

        unitOfWork.Users.Remove(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("User account deleted: {Email}", user.Email);
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
            retryAfter);
    }

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

        var local = user.Email.Split('@')[0];
        return string.IsNullOrWhiteSpace(local) ? "user" : local;
    }

    private static string NormalizeNicknameInput(string nickname) =>
        Regex.Replace(nickname.Trim(), @"\s+", "-");

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

    private sealed record FuelPriceCell(decimal? Before, decimal? After, decimal? Percent);
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

        await emailSender.SendEmailAsync(
            subscription.User.Email,
            "LiveFuelMap: підписку оформлено",
            BuildSubscriptionConfirmationEmail(subscription),
            isHtml: true,
            cancellationToken: cancellationToken);

        var selectedFuelIds = await GetUserSubscribedFuelIdsAsync(userId, subscription.City, cancellationToken);
        var table = await BuildFuelPriceTableAsync(subscription.City, selectedFuelIds, [], cancellationToken);

        await emailSender.SendEmailAsync(
            subscription.User.Email,
            $"LiveFuelMap: таблиця цін за підпискою ({DisplayCity(subscription.City)})",
            BuildFuelDigestEmail(
                "Ваші вибрані типи палива",
                "Нижче таблиця з актуальними цінами, попередніми значеннями та відсотком зміни по АЗС.",
                table),
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
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Fuel)
            .Where(x =>
                affectedFuelIds.Contains(x.FuelId) &&
                affectedCities.Contains(x.City) &&
                x.User.EmailConfirmed)
            .ToListAsync(cancellationToken);

        foreach (var userCityGroup in subscriptions.GroupBy(x => new { x.UserId, x.User.Email, x.City }))
        {
            var selectedFuelIds = await GetUserSubscribedFuelIdsAsync(userCityGroup.Key.UserId, userCityGroup.Key.City, cancellationToken);
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
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send price change email to {Email}", userCityGroup.Key.Email);
            }
        }
    }

    private async Task<IReadOnlyList<int>> GetUserSubscribedFuelIdsAsync(int userId, string city, CancellationToken cancellationToken)
    {
        var normalizedCity = NormalizeCity(city);
        return await unitOfWork.Subscriptions.Query()
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.City == normalizedCity)
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

                    if (changes.TryGetValue((station.Id, fuel.Id), out var change))
                    {
                        before = change.OldPrice;
                        after = change.NewPrice;
                    }
                    else if (history.TryGetValue((station.Id, fuel.Id), out var stationFuelPrices))
                    {
                        after = stationFuelPrices.ElementAtOrDefault(0)?.Price;
                        before = stationFuelPrices.ElementAtOrDefault(1)?.Price;
                    }

                    var percent = CalculatePercent(before, after);
                    return new FuelPriceCell(before, after, percent);
                });

            return new FuelPriceTableRow(station.Name, cells);
        }).ToList();

        return new FuelPriceTable(DisplayCity(normalizedCity), fuels, rows);
    }

    private static string BuildSubscriptionConfirmationEmail(Subscription subscription)
    {
        var fuelName = DisplayFuelName(subscription.Fuel);
        var city = DisplayCity(subscription.City);
        return BuildEmailShell(
            "Підписку оформлено",
            $"""
            <p style="margin:0 0 18px;font-size:16px;line-height:1.6;color:#52666d;">
                Дякуємо за оформлення підписки на розсилку повідомлень LiveFuelMap.
            </p>
            <div style="background:#f5fbfb;border:1px solid #cbe4e5;border-radius:14px;padding:18px;text-align:left;">
                <div style="font-size:14px;color:#637176;">Місто</div>
                <div style="font-size:18px;font-weight:700;color:{PrimaryColor};margin-bottom:12px;">{WebUtility.HtmlEncode(city)}</div>
                <div style="font-size:14px;color:#637176;">Тип палива</div>
                <div style="font-size:18px;font-weight:700;color:{AccentColor};">{WebUtility.HtmlEncode(fuelName)}</div>
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
            $"""<th colspan="3" style="padding:12px 10px;background:{PrimaryColor};color:#ffffff;border:1px solid {PrimaryColor};font-size:13px;text-align:center;">{WebUtility.HtmlEncode(DisplayFuelName(fuel))}</th>"""));

        var subHeaders = string.Concat(table.Fuels.Select(_ =>
            $"""<th style="padding:9px 8px;background:#eef6f7;color:{PrimaryColor};border:1px solid {BorderColor};font-size:12px;text-align:right;">До</th><th style="padding:9px 8px;background:#eef6f7;color:{PrimaryColor};border:1px solid {BorderColor};font-size:12px;text-align:right;">Після</th><th style="padding:9px 8px;background:#eef6f7;color:{PrimaryColor};border:1px solid {BorderColor};font-size:12px;text-align:right;">%</th>"""));

        var rows = table.Rows.Count == 0
            ? $"""<tr><td colspan="{1 + table.Fuels.Count * 3}" style="padding:18px;border:1px solid {BorderColor};text-align:center;color:#637176;">Немає АЗС для вибраного міста.</td></tr>"""
            : string.Concat(table.Rows.Select((row, index) =>
            {
                var background = index % 2 == 0 ? "#ffffff" : "#f8fbfc";
                var cells = string.Concat(table.Fuels.Select(fuel =>
                {
                    var cell = row.Cells[fuel.Id];
                    return $"""
                        <td style="padding:10px 8px;background:{background};border:1px solid {BorderColor};font-size:13px;text-align:right;white-space:nowrap;">{FormatPrice(cell.Before)}</td>
                        <td style="padding:10px 8px;background:{background};border:1px solid {BorderColor};font-size:13px;text-align:right;white-space:nowrap;font-weight:700;color:{PrimaryColor};">{FormatPrice(cell.After)}</td>
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
                <table role="table" cellspacing="0" cellpadding="0" style="width:100%;min-width:{Math.Max(560, 160 + table.Fuels.Count * 210)}px;border-collapse:collapse;border:1px solid {BorderColor};border-radius:12px;overflow:hidden;">
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

    private static decimal? CalculatePercent(decimal? before, decimal? after)
    {
        if (before is null || after is null || before.Value == 0)
            return null;

        return Math.Round((after.Value - before.Value) / before.Value * 100m, 2);
    }

    private static string FormatPrice(decimal? price) =>
        price is null ? "—" : price.Value.ToString("0.00", CultureInfo.InvariantCulture);

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
