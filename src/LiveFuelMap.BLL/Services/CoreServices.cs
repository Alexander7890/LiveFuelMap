using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.DAL.Entities;
using LiveFuelMap.DAL.Enums;
using LiveFuelMap.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LiveFuelMap.BLL.Services;

public sealed class PriceChangeDetector : IPriceChangeDetector
{
    public bool IsMeaningfulChange(decimal? latestPrice, decimal newPrice) =>
        latestPrice is null || Math.Abs(latestPrice.Value - newPrice) >= 0.01m;
}

public sealed class NicknameUnavailableException(string message, IReadOnlyList<string> suggestions) : InvalidOperationException(message)
{
    public IReadOnlyList<string> Suggestions { get; } = suggestions;
}

public sealed class CaptchaVerificationException(string message) : InvalidOperationException(message);

public sealed class ValidationFailedException(string message, IReadOnlyDictionary<string, string[]> errors) : InvalidOperationException(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class AuthenticationFailedException(string message, IReadOnlyDictionary<string, string[]> errors) : InvalidOperationException(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class AuthService(
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenHasher tokenHasher,
    ITokenService tokenService,
    ICaptchaVerificationService captchaVerificationService,
    IEmailSender emailSender,
    IOptions<JwtOptions> jwtOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private static readonly TimeSpan EmailVerificationCodeLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan EmailVerificationResendDelay = TimeSpan.FromMinutes(1);
    private static readonly Regex EmailRegex = new(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> ObviousPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456",
        "12345678",
        "123456789",
        "password",
        "qwerty",
        "111111",
        "admin",
        "admin123"
    };
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var email = NormalizeEmail(request.Email);
        var displayName = NormalizeDisplayName(request.DisplayName);
        var nickname = NormalizeNicknameInput(request.Nickname);
        var normalizedNickname = NormalizeNickname(nickname);

        ValidateEmail(email, errors);

        if (displayName.Length < 2)
            AddError(errors, "displayName", "Ім'я має містити мінімум 2 символи.");

        if (!IsValidNickname(nickname))
            AddError(errors, "nickname", "Нікнейм має містити 3-24 літери, цифри, дефіс або underscore.");

        ValidateRegistrationPassword(request.Password, errors);

        if (string.IsNullOrWhiteSpace(request.ConfirmPassword))
            AddError(errors, "confirmPassword", "Повторіть пароль.");
        else if (request.Password != request.ConfirmPassword)
            AddError(errors, "confirmPassword", "Паролі не збігаються.");

        ThrowIfValidationFailed(errors);

        await captchaVerificationService.VerifyAsync(request.CaptchaToken, cancellationToken);

        if (await unitOfWork.Users.ExistsAsync(x => x.Email == email, cancellationToken))
        {
            AddError(errors, "email", "Користувач з таким email вже існує.");
            ThrowIfValidationFailed(errors, "Не вдалося створити акаунт.");
        }

        if (await unitOfWork.Users.ExistsAsync(x => x.NormalizedNickname == normalizedNickname, cancellationToken))
        {
            var suggestions = await SuggestNicknamesAsync(nickname, 3, cancellationToken);
            throw new NicknameUnavailableException("Цей нікнейм уже використовується.", suggestions);
        }

        var now = DateTime.UtcNow;
        var verificationCode = CreateVerificationCode();
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            Nickname = nickname,
            NormalizedNickname = normalizedNickname,
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = UserRole.User,
            EmailConfirmed = false,
            EmailConfirmationTokenHash = tokenHasher.Hash(verificationCode),
            EmailConfirmationTokenExpiresAt = now.Add(EmailVerificationCodeLifetime),
            LastEmailConfirmationSentAt = now
        };

        await unitOfWork.Users.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await SendVerificationEmailAsync(user.Email, verificationCode, user.EmailConfirmationTokenExpiresAt.Value, cancellationToken);
        logger.LogInformation("User registered: {Email}", email);
    }

    public async Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var email = NormalizeEmail(request.Email);
        ValidateEmail(email, errors);

        if (string.IsNullOrWhiteSpace(request.Password))
            AddError(errors, "password", "Введіть пароль.");
        else if (request.Password.Length < 6)
            AddError(errors, "password", "Пароль має містити мінімум 6 символів.");

        ThrowIfValidationFailed(errors);

        await captchaVerificationService.VerifyAsync(request.CaptchaToken, cancellationToken);

        var user = await unitOfWork.Users.Query()
            .FirstOrDefaultAsync(x => x.Email == email && !x.IsDeleted, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("Failed login for unknown email {Email}", email);
            throw new AuthenticationFailedException(
                "Користувача з таким email не знайдено.",
                ToErrorDictionary(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["email"] = ["Користувача з таким email не знайдено."]
                }));
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Failed login for {Email}", email);
            throw new AuthenticationFailedException(
                "Невірний пароль.",
                ToErrorDictionary(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["password"] = ["Невірний пароль."]
                }));
        }

        user.LastLoginAt = DateTime.UtcNow;
        var result = IssueTokens(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Successful login for {Email}", email);
        return result;
    }

    public async Task<AuthResultDto> LoginWithGoogleAsync(GoogleAccountDto googleAccount, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(googleAccount.Email))
            throw new InvalidOperationException("Google account does not contain an email address.");

        if (!googleAccount.EmailVerified)
            throw new InvalidOperationException("Google account email is not verified.");

        if (string.IsNullOrWhiteSpace(googleAccount.ProviderUserId))
            throw new InvalidOperationException("Google account identifier is missing.");

        var email = googleAccount.Email.Trim().ToLowerInvariant();
        var providerUserId = googleAccount.ProviderUserId.Trim();

        var user = await unitOfWork.Users.Query()
            .FirstOrDefaultAsync(x =>
                !x.IsDeleted &&
                ((x.AuthProvider == "Google" && x.ExternalProviderId == providerUserId) ||
                x.Email == email),
                cancellationToken);

        var isNewUser = false;
        if (user is null)
        {
            isNewUser = true;
            var googleName = NormalizeDisplayName(googleAccount.DisplayName);
            if (googleName.Length < 2)
                googleName = null;

            user = new User
            {
                Email = email,
                DisplayName = null,
                Nickname = null,
                NormalizedNickname = null,
                ProfileImageUrl = NormalizeGoogleProfileImageUrl(googleAccount.ProfileImageUrl),
                PasswordHash = passwordHasher.Hash(CreateSecret()),
                Role = UserRole.User,
                EmailConfirmed = true,
                AuthProvider = "Google",
                ExternalProviderId = providerUserId,
                GoogleName = googleName,
                RequiresNicknameSetup = true
            };

            await unitOfWork.Users.AddAsync(user, cancellationToken);
        }
        else
        {
            user.AuthProvider = "Google";
            user.ExternalProviderId = providerUserId;
            user.EmailConfirmed = true;
            user.EmailConfirmationTokenHash = null;
            user.EmailConfirmationTokenExpiresAt = null;
            user.LastEmailConfirmationSentAt = null;

            var googleName = NormalizeDisplayName(googleAccount.DisplayName);
            if (!string.IsNullOrWhiteSpace(googleName))
                user.GoogleName = googleName;

            var profileImageUrl = NormalizeGoogleProfileImageUrl(googleAccount.ProfileImageUrl);
            if (!string.IsNullOrWhiteSpace(profileImageUrl) && string.IsNullOrWhiteSpace(user.ProfileImageUrl))
                user.ProfileImageUrl = profileImageUrl;

            if (string.Equals(user.AuthProvider, "Google", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(user.Nickname))
            {
                user.RequiresNicknameSetup = true;
            }
        }

        user.LastLoginAt = DateTime.UtcNow;
        if (isNewUser)
            await unitOfWork.SaveChangesAsync(cancellationToken);

        var result = IssueTokens(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Successful Google login for {Email}", email);
        return result with
        {
            RequiresNickname = user.RequiresNicknameSetup,
            SuggestedNickname = user.RequiresNicknameSetup ? CreateNicknameSuggestionFromEmail(email) : null
        };
    }

    public async Task<AuthResultDto> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new InvalidOperationException("Refresh token is required.");

        var refreshTokenHash = tokenHasher.Hash(request.RefreshToken);
        var now = DateTime.UtcNow;
        var user = await unitOfWork.Users.Query()
            .FirstOrDefaultAsync(x => x.RefreshTokenHash == refreshTokenHash, cancellationToken);

        if (user is null ||
            user.IsDeleted ||
            user.RefreshTokenRevokedAt is not null ||
            user.RefreshTokenExpiresAt is null ||
            user.RefreshTokenExpiresAt <= now)
        {
            logger.LogWarning("Rejected refresh token.");
            throw new InvalidOperationException("Invalid refresh token.");
        }

        var result = IssueTokens(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Refresh token rotated for {Email}", user.Email);
        return result;
    }

    public async Task LogoutAsync(int userId, LogoutRequest request, CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (user.IsDeleted)
            throw new InvalidOperationException("User not found.");

        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAt = null;
        user.RefreshTokenRevokedAt = DateTime.UtcNow;
        user.TokenVersion++;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("User logged out: {Email}", user.Email);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword != request.ConfirmNewPassword)
        {
            throw new InvalidOperationException("Password data is invalid.");
        }

        if (request.NewPassword.Length < 6)
            throw new InvalidOperationException("Password must contain at least 6 characters.");

        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (user.IsDeleted)
            throw new InvalidOperationException("User not found.");

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            logger.LogWarning("Failed password change for {Email}", user.Email);
            throw new InvalidOperationException("Поточний пароль неправильний.");
        }

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAt = null;
        user.RefreshTokenRevokedAt = DateTime.UtcNow;
        user.TokenVersion++;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Password changed for {Email}", user.Email);
    }

    public async Task VerifyEmailAsync(int userId, VerifyEmailRequest request, CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (user.IsDeleted)
            throw new InvalidOperationException("User not found.");

        if (user.EmailConfirmed)
            return;

        if (string.IsNullOrWhiteSpace(request.Code) ||
            string.IsNullOrWhiteSpace(user.EmailConfirmationTokenHash) ||
            user.EmailConfirmationTokenExpiresAt is null ||
            user.EmailConfirmationTokenExpiresAt <= DateTime.UtcNow ||
            !tokenHasher.Verify(request.Code.Trim(), user.EmailConfirmationTokenHash))
        {
            throw new InvalidOperationException("Verification code is invalid or expired.");
        }

        user.EmailConfirmed = true;
        user.EmailConfirmationTokenHash = null;
        user.EmailConfirmationTokenExpiresAt = null;
        user.LastEmailConfirmationSentAt = null;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Email verified for {Email}", user.Email);
    }

    public async Task ResendEmailVerificationCodeAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (user.IsDeleted)
            throw new InvalidOperationException("User not found.");

        if (user.EmailConfirmed)
            throw new InvalidOperationException("Email is already verified.");

        var now = DateTime.UtcNow;
        if (user.LastEmailConfirmationSentAt is not null &&
            user.LastEmailConfirmationSentAt.Value.Add(EmailVerificationResendDelay) > now)
        {
            var retryAt = user.LastEmailConfirmationSentAt.Value.Add(EmailVerificationResendDelay);
            throw new InvalidOperationException($"Please wait until {retryAt:yyyy-MM-dd HH:mm:ss} UTC before requesting a new code.");
        }

        var verificationCode = CreateVerificationCode();
        user.EmailConfirmationTokenHash = tokenHasher.Hash(verificationCode);
        user.EmailConfirmationTokenExpiresAt = now.Add(EmailVerificationCodeLifetime);
        user.LastEmailConfirmationSentAt = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await SendVerificationEmailAsync(user.Email, verificationCode, user.EmailConfirmationTokenExpiresAt.Value, cancellationToken);
        logger.LogInformation("Verification code resent for {Email}", user.Email);
    }

    public async Task<IReadOnlyList<string>> SuggestNicknamesAsync(string nickname, int count = 3, CancellationToken cancellationToken = default)
    {
        count = Math.Clamp(count, 1, 3);
        var baseNickname = NormalizeNicknameInput(nickname);
        baseNickname = Regex.Replace(baseNickname, @"[^\p{L}\p{Nd}_-]+", string.Empty);
        if (baseNickname.Length < 3)
            baseNickname = "user";
        if (baseNickname.Length > 18)
            baseNickname = baseNickname[..18];

        var suggestions = new List<string>(count);
        var candidates = new Queue<string>([
            $"{baseNickname}{RandomNumberGenerator.GetInt32(10, 100)}",
            $"{baseNickname}_map",
            $"{baseNickname}{DateTime.UtcNow:yy}",
            $"{baseNickname}_fuel",
            $"{baseNickname}{RandomNumberGenerator.GetInt32(100, 1000)}"
        ]);

        while (suggestions.Count < count && candidates.Count > 0)
        {
            var candidate = candidates.Dequeue();
            if (candidate.Length > 24)
                candidate = candidate[..24].TrimEnd('_', '-');

            var normalized = NormalizeNickname(candidate);
            if (suggestions.Any(x => NormalizeNickname(x) == normalized))
                continue;

            var exists = await unitOfWork.Users.ExistsAsync(x => x.NormalizedNickname == normalized, cancellationToken);
            if (!exists)
                suggestions.Add(candidate);
        }

        return suggestions;
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (user.IsDeleted)
            throw new InvalidOperationException("User not found.");

        return new CurrentUserDto(
            user.Id,
            user.Email,
            user.Role.ToString(),
            user.EmailConfirmed,
            GetDisplayName(user),
            GetNickname(user),
            GetProfileImageUrl(user),
            user.EmailConfirmed,
            user.AuthProvider,
            user.RequiresNicknameSetup);
    }

    private static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    private static void ValidateEmail(string email, IDictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            AddError(errors, "email", "Введіть email.");
            return;
        }

        if (!EmailRegex.IsMatch(email))
            AddError(errors, "email", "Введіть коректний email.");
    }

    private static void ValidateRegistrationPassword(string? password, IDictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            AddError(errors, "password", "Введіть пароль.");
            return;
        }

        if (password.Length < 8)
            AddError(errors, "password", "Пароль має містити мінімум 8 символів.");

        var categories = 0;
        if (password.Any(char.IsLower)) categories++;
        if (password.Any(char.IsUpper)) categories++;
        if (password.Any(char.IsDigit)) categories++;
        if (password.Any(ch => !char.IsLetterOrDigit(ch))) categories++;

        if (categories < 2)
            AddError(errors, "password", "Додайте до пароля літери різного регістру, цифри або спецсимволи.");

        if (ObviousPasswords.Contains(password.Trim()))
            AddError(errors, "password", "Оберіть менш очевидний пароль.");
    }

    private static void AddError(IDictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var items))
        {
            items = [];
            errors[field] = items;
        }

        items.Add(message);
    }

    private static void ThrowIfValidationFailed(Dictionary<string, List<string>> errors, string message = "Перевірте правильність заповнення форми.")
    {
        if (errors.Count > 0)
            throw new ValidationFailedException(message, ToErrorDictionary(errors));
    }

    private static IReadOnlyDictionary<string, string[]> ToErrorDictionary(Dictionary<string, List<string>> errors) =>
        errors.ToDictionary(pair => pair.Key, pair => pair.Value.Distinct().ToArray(), StringComparer.OrdinalIgnoreCase);

    private static string CreateSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string CreateVerificationCode() =>
        RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    private static string CreateDefaultDisplayName(string email)
    {
        var local = email.Split('@')[0].Trim();
        return string.IsNullOrWhiteSpace(local) ? "User" : local;
    }

    private static string GetDisplayName(User user) =>
        string.IsNullOrWhiteSpace(user.DisplayName) ? CreateDefaultDisplayName(user.Email) : user.DisplayName.Trim();

    private static string GetNickname(User user) =>
        string.IsNullOrWhiteSpace(user.Nickname) ? string.Empty : user.Nickname.Trim();

    private static string GetProfileImageUrl(User user) =>
        string.IsNullOrWhiteSpace(user.ProfileImageUrl) ? "default-station.jpg" : user.ProfileImageUrl.Trim();

    private static string NormalizeDisplayName(string? displayName) =>
        Regex.Replace((displayName ?? string.Empty).Trim(), @"\s+", " ");

    private static string NormalizeNicknameInput(string? nickname) =>
        (nickname ?? string.Empty).Trim();

    private static string NormalizeNickname(string nickname) =>
        nickname.Trim().ToLowerInvariant();

    private static bool IsValidNickname(string nickname) =>
        Regex.IsMatch(nickname, @"^[\p{L}\p{Nd}_-]{3,24}$");

    private static string CreateNicknameSuggestionFromEmail(string email)
    {
        var localPart = email.Split('@')[0];
        var baseNickname = NormalizeNicknameInput(localPart);
        baseNickname = Regex.Replace(baseNickname, @"[^\p{L}\p{Nd}_-]+", string.Empty);
        baseNickname = Regex.Replace(baseNickname, @"[-_]{2,}", "-").Trim('-', '_');

        if (baseNickname.Length < 3)
            return "user";

        return baseNickname.Length > 24 ? baseNickname[..24].Trim('-', '_') : baseNickname;
    }

    private static string? NormalizeGoogleProfileImageUrl(string? profileImageUrl)
    {
        var normalized = profileImageUrl?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 500)
            return null;

        return Uri.TryCreate(normalized, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? normalized
            : null;
    }

    private Task SendVerificationEmailAsync(string email, string code, DateTime expiresAt, CancellationToken cancellationToken)
    {
        var body = $$"""
            <!doctype html>
            <html lang="uk">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>LiveFuelMap email verification</title>
            </head>
            <body style="margin:0;padding:0;background:#edf4f6;font-family:'Segoe UI',Arial,sans-serif;color:#20323a;">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#edf4f6;padding:32px 12px;">
                    <tr>
                        <td align="center">
                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#ffffff;border:1px solid #d9e1e4;border-radius:18px;overflow:hidden;box-shadow:0 18px 38px rgba(25,39,45,.16);">
                                <tr>
                                    <td style="background:#20323a;padding:26px 28px;text-align:center;">
                                        <div style="font-size:15px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;color:#bde7e7;">LiveFuelMap</div>
                                        <div style="margin-top:8px;font-size:24px;line-height:1.25;font-weight:700;color:#ffffff;">Підтвердження email</div>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="padding:34px 30px 30px;text-align:center;">
                                        <div style="font-size:18px;line-height:1.5;font-weight:600;color:#20323a;">Ваш код підтвердження</div>
                                        <div style="margin:22px auto 24px;padding:20px 28px;display:inline-block;min-width:260px;border-radius:16px;background:#f5fbfb;border:2px solid #0f8b8d;color:#0f5658;font-size:44px;line-height:1;font-weight:800;letter-spacing:.18em;text-align:center;">
                                            {{code}}
                                        </div>
                                        <div style="font-size:15px;line-height:1.6;color:#52666d;">
                                            Введіть цей код на сайті LiveFuelMap, щоб підтвердити акаунт.
                                        </div>
                                        <div style="margin-top:18px;padding:14px 16px;border-radius:12px;background:#f8fafb;border:1px solid #e2e9eb;font-size:14px;line-height:1.5;color:#637176;">
                                            Код діє до <strong style="color:#20323a;">{{expiresAt:yyyy-MM-dd HH:mm:ss}} UTC</strong>.
                                        </div>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="padding:0 30px 30px;text-align:center;">
                                        <div style="border-top:1px solid #edf1f2;padding-top:20px;font-size:13px;line-height:1.5;color:#7a888d;">
                                            Якщо ви не створювали акаунт у LiveFuelMap, просто проігноруйте цей лист.
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

        return emailSender.SendEmailAsync(
            email,
            "LiveFuelMap: код підтвердження email",
            body,
            isHtml: true,
            cancellationToken: cancellationToken);
    }

    private AuthResultDto IssueTokens(User user)
    {
        var accessToken = tokenService.CreateAccessToken(user);
        var refreshToken = CreateSecret();
        var refreshExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);

        user.RefreshTokenHash = tokenHasher.Hash(refreshToken);
        user.RefreshTokenExpiresAt = refreshExpiresAt;
        user.RefreshTokenRevokedAt = null;

        return new AuthResultDto(
            user.Id,
            user.Email,
            user.Role.ToString(),
            accessToken.Token,
            refreshToken,
            accessToken.ExpiresAt,
            refreshExpiresAt,
            RequiresNickname: user.RequiresNicknameSetup,
            SuggestedNickname: user.RequiresNicknameSetup ? CreateNicknameSuggestionFromEmail(user.Email) : null,
            AuthProvider: user.AuthProvider);
    }
}

public sealed class FuelDataService(IUnitOfWork unitOfWork) : IFuelDataService
{
    private static readonly string[] Colors = ["#ff6384", "#36a2eb", "#ffce56", "#4bc0c0", "#9966ff", "#2ecc71"];

    public async Task<IReadOnlyList<FuelDto>> GetFuelsAsync(CancellationToken cancellationToken = default)
    {
        return await unitOfWork.Fuels.Query()
            .OrderBy(x => x.SortOrder)
            .Select(x => new FuelDto(x.Id, x.Code, x.Name, x.SortOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<StationDto>> GetStationsAsync(StationListQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var stations = await unitOfWork.Stations.SearchAsync(query.City, query.Name, page, pageSize, cancellationToken);
        var total = await unitOfWork.Stations.CountAsync(query.City, query.Name, cancellationToken);
        var stationDtos = await BuildStationDtosAsync(stations, query.FuelCode, cancellationToken);
        return new PagedResult<StationDto>(stationDtos, page, pageSize, total);
    }

    public async Task<StationDto?> GetStationAsync(int id, CancellationToken cancellationToken = default)
    {
        var station = await unitOfWork.Stations.Query().FirstOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);
        if (station is null) return null;
        return (await BuildStationDtosAsync([station], null, cancellationToken)).Single();
    }

    public async Task<PriceHistoryDto> GetPriceHistoryAsync(PriceHistoryQuery query, CancellationToken cancellationToken = default)
    {
        IQueryable<FuelPrice> prices = unitOfWork.FuelPrices.Query()
            .Include(x => x.Fuel)
            .Include(x => x.Station);

        if (query.StationId is not null)
            prices = prices.Where(x => x.StationId == query.StationId.Value);

        var fuelFilter = query.Fuel?.Trim();
        if (!string.IsNullOrWhiteSpace(fuelFilter) && !string.Equals(fuelFilter, "all", StringComparison.OrdinalIgnoreCase))
            prices = prices.Where(x => x.Fuel.Code == fuelFilter || x.Fuel.Name == fuelFilter);

        var to = query.To?.Date;
        if (to is null)
        {
            to = await prices
                .Select(x => (DateTime?)x.Date)
                .MaxAsync(cancellationToken);

            if (to is null)
                return new PriceHistoryDto([], []);
        }

        var from = (query.From ?? to.Value.AddMonths(-1)).Date;
        var historyRows = await prices
            .Where(x => x.Date <= to.Value)
            .Select(x => new { x.Date, FuelId = x.Fuel.Id, Fuel = x.Fuel.Name, x.Fuel.Code, x.Fuel.SortOrder, StationId = x.Station.Id, Station = x.Station.Name, x.Price })
            .ToListAsync(cancellationToken);

        var labelDates = historyRows
            .Where(x => x.Date >= from && x.Date <= to.Value)
            .Select(x => x.Date.Date)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (labelDates.Count == 0)
            return new PriceHistoryDto([], []);

        var labels = labelDates.Select(x => x.ToString("yyyy-MM-dd")).ToList();

        if (string.IsNullOrWhiteSpace(fuelFilter) || string.Equals(fuelFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            var datasets = historyRows
                .GroupBy(x => new { x.FuelId, x.Fuel, x.SortOrder })
                .OrderBy(x => x.Key.SortOrder)
                .Select((group, index) => new ChartDatasetDto(
                    group.Key.Fuel,
                    labelDates.Select(labelDate =>
                    {
                        var stationPrices = group
                            .GroupBy(x => x.StationId)
                            .Select(stationGroup => stationGroup
                                .Where(x => x.Date.Date <= labelDate)
                                .OrderByDescending(x => x.Date)
                                .FirstOrDefault()?.Price)
                            .Where(x => x is not null)
                            .Select(x => x!.Value)
                            .ToList();

                        return stationPrices.Count == 0 ? (decimal?)null : Math.Round(stationPrices.Average(), 2);
                    }).ToList(),
                    Colors[index % Colors.Length]))
                .ToList();

            return new PriceHistoryDto(labels, datasets);
        }

        var stationDatasets = historyRows
            .GroupBy(x => new { x.StationId, x.Station })
            .OrderBy(x => x.Key.Station)
            .Select((group, index) => new ChartDatasetDto(
                group.Key.Station,
                labelDates.Select(labelDate =>
                {
                    return group
                        .Where(x => x.Date.Date <= labelDate)
                        .OrderByDescending(x => x.Date)
                        .Select(x => (decimal?)x.Price)
                        .FirstOrDefault();
                }).ToList(),
                Colors[index % Colors.Length]))
            .ToList();

        return new PriceHistoryDto(labels, stationDatasets);
    }

    public async Task<CompareResultDto> CompareStationsAsync(IReadOnlyList<int> stationIds, CancellationToken cancellationToken = default)
    {
        if (stationIds.Count < 2)
            throw new InvalidOperationException("Select at least two stations.");

        var fuels = await GetFuelsAsync(cancellationToken);
        var stations = await unitOfWork.Stations.Query()
            .Where(x => stationIds.Contains(x.Id) && x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var stationDtos = await BuildStationDtosAsync(stations, null, cancellationToken);
        var compareStations = stationDtos.Select(x => new CompareStationDto(
            x.Id,
            x.Name,
            x.Address,
            x.City,
            x.Latitude,
            x.Longitude,
            x.Prices.ToDictionary(p => p.FuelId))).ToList();

        return new CompareResultDto(fuels, compareStations);
    }

    public async Task<FuelPriceCorrectionResultDto> CorrectFuelPriceAsync(UpsertFuelPriceRequest request, CancellationToken cancellationToken = default)
    {
        var station = await unitOfWork.Stations.GetByIdAsync(request.StationId, cancellationToken)
            ?? throw new InvalidOperationException("Station not found.");
        var fuel = await unitOfWork.Fuels.GetByIdAsync(request.FuelId, cancellationToken)
            ?? throw new InvalidOperationException("Fuel not found.");

        var date = request.Date.Date;
        var existing = await unitOfWork.FuelPrices.Query()
            .FirstOrDefaultAsync(x => x.StationId == station.Id && x.FuelId == fuel.Id && x.Date == date, cancellationToken);

        decimal? oldPrice;
        if (existing is not null)
        {
            oldPrice = existing.Price;
            existing.Price = request.Price;
            existing.Popularity = request.Popularity;
            existing.IsManual = true;
        }
        else
        {
            oldPrice = await unitOfWork.FuelPrices.Query()
                .Where(x => x.StationId == station.Id && x.FuelId == fuel.Id)
                .OrderByDescending(x => x.Date)
                .ThenByDescending(x => x.Id)
                .Select(x => (decimal?)x.Price)
                .FirstOrDefaultAsync(cancellationToken);

            await unitOfWork.FuelPrices.AddAsync(new FuelPrice
            {
                StationId = station.Id,
                FuelId = fuel.Id,
                Date = date,
                Price = request.Price,
                Popularity = request.Popularity,
                IsManual = true
            }, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        var price = new FuelPriceDto(fuel.Id, fuel.Code, fuel.Name, request.Price, request.Popularity, date);
        var change = oldPrice == request.Price
            ? null
            : new PriceChangeNotificationDto(
                station.Id,
                station.Name,
                station.City,
                fuel.Id,
                fuel.Code,
                fuel.Name,
                oldPrice,
                request.Price,
                GetChangeType(oldPrice, request.Price),
                date);

        return new FuelPriceCorrectionResultDto(price, change);
    }

    private static string GetChangeType(decimal? oldPrice, decimal newPrice)
    {
        if (oldPrice is null) return "new";
        return newPrice > oldPrice.Value ? "increase" : "decrease";
    }

    private async Task<IReadOnlyList<StationDto>> BuildStationDtosAsync(IReadOnlyList<Station> stations, string? fuelCode, CancellationToken cancellationToken)
    {
        var stationIds = stations.Select(x => x.Id).ToList();
        var fuels = await unitOfWork.Fuels.Query().OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
        var prices = await unitOfWork.FuelPrices.Query()
            .Include(x => x.Fuel)
            .Where(x => stationIds.Contains(x.StationId))
            .ToListAsync(cancellationToken);

        var latestPrices = prices
            .GroupBy(x => new { x.StationId, x.FuelId })
            .Select(x => x.OrderByDescending(p => p.Date).ThenByDescending(p => p.Id).First())
            .Where(x => string.IsNullOrWhiteSpace(fuelCode) || x.Fuel.Code == fuelCode)
            .ToList();
        var ratings = await unitOfWork.Comments.Query()
            .AsNoTracking()
            .Where(x => stationIds.Contains(x.StationId))
            .GroupBy(x => x.StationId)
            .Select(x => new
            {
                StationId = x.Key,
                AverageRating = x.Average(comment => comment.Rating),
                ReviewCount = x.Count()
            })
            .ToDictionaryAsync(x => x.StationId, cancellationToken);

        return stations.Select(station =>
        {
            var stationPrices = latestPrices
                .Where(x => x.StationId == station.Id)
                .OrderBy(x => x.Fuel.SortOrder)
                .Select(x => new FuelPriceDto(x.FuelId, x.Fuel.Code, x.Fuel.Name, x.Price, x.Popularity, x.Date))
                .ToList();

            var priceMap = fuels.ToDictionary(
                fuel => fuel.Code,
                fuel => stationPrices.FirstOrDefault(x => x.FuelId == fuel.Id)?.Price);

            return new StationDto(
                station.Id,
                station.Name,
                station.Address,
                station.City,
                station.Latitude,
                station.Longitude,
                string.IsNullOrWhiteSpace(station.ImageUrl) ? "default-station.jpg" : station.ImageUrl,
                string.IsNullOrWhiteSpace(station.WebsiteUrl) ? null : station.WebsiteUrl.Trim(),
                StationPhotoUrls.Parse(station.PhotoUrls),
                ratings.TryGetValue(station.Id, out var rating)
                    ? Math.Round((decimal)rating.AverageRating, 1)
                    : null,
                ratings.TryGetValue(station.Id, out var ratingCount) ? ratingCount.ReviewCount : 0,
                stationPrices,
                priceMap);
        }).ToList();
    }
}

public sealed class CommentService(IUnitOfWork unitOfWork) : ICommentService
{
    private static readonly TimeSpan NewAccountCommentDelay = TimeSpan.FromMinutes(10);

    public async Task<PagedResult<AdminCommentDto>> ListAdminAsync(CommentAdminQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var commentsQuery = unitOfWork.Comments.Query()
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Station)
            .Include(x => x.Fuel)
            .AsQueryable();

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            commentsQuery = commentsQuery.Where(x =>
                EF.Functions.Like(x.Content, pattern) ||
                EF.Functions.Like(x.User.Email, pattern) ||
                (x.User.Nickname != null && EF.Functions.Like(x.User.Nickname, pattern)) ||
                (x.User.NormalizedNickname != null && EF.Functions.Like(x.User.NormalizedNickname, pattern)) ||
                (x.User.DisplayName != null && EF.Functions.Like(x.User.DisplayName, pattern)) ||
                EF.Functions.Like(x.Station.Name, pattern) ||
                (x.Fuel != null && EF.Functions.Like(x.Fuel.Name, pattern)));
        }

        var author = query.Author?.Trim().ToLowerInvariant();
        commentsQuery = author switch
        {
            "registered" => commentsQuery.Where(x => !x.User.IsDeleted),
            "deleted" => commentsQuery.Where(x => x.User.IsDeleted),
            "guest" => commentsQuery.Where(_ => false),
            _ => commentsQuery
        };

        var status = query.Status?.Trim().ToLowerInvariant();
        commentsQuery = status switch
        {
            null or "" or "published" or "active" => commentsQuery,
            _ => commentsQuery.Where(_ => false)
        };

        if (query.DateFrom is not null)
        {
            var dateFrom = query.DateFrom.Value.Date;
            commentsQuery = commentsQuery.Where(x => x.CreatedAt >= dateFrom);
        }

        if (query.DateTo is not null)
        {
            var dateToExclusive = query.DateTo.Value.Date.AddDays(1);
            commentsQuery = commentsQuery.Where(x => x.CreatedAt < dateToExclusive);
        }

        var totalCount = await commentsQuery.CountAsync(cancellationToken);
        var comments = await commentsQuery
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminCommentDto>(
            comments.Select(ToAdminDto).ToList(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<IReadOnlyList<CommentDto>> ListAsync(int? stationId = null, string? search = null, int take = 200, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 500);
        var query = unitOfWork.Comments.Query()
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Station)
            .Include(x => x.Fuel)
            .AsQueryable();

        if (stationId is not null)
            query = query.Where(x => x.StationId == stationId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.Content.Contains(term) ||
                x.User.Email.Contains(term) ||
                x.Station.Name.Contains(term) ||
                (x.Fuel != null && x.Fuel.Name.Contains(term)));
        }

        var comments = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        return comments.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<CommentDto>> GetByStationAsync(int stationId, CancellationToken cancellationToken = default)
    {
        if (!await unitOfWork.Stations.ExistsAsync(x => x.Id == stationId && x.IsActive, cancellationToken))
            return [];

        var comments = await unitOfWork.Comments.Query()
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Station)
            .Include(x => x.Fuel)
            .Where(x => x.StationId == stationId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        return comments.Select(ToDto).ToList();
    }

    public async Task<CommentDto> CreateAsync(int userId, CreateCommentRequest request, CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (user.IsDeleted)
            throw new InvalidOperationException("User not found.");

        if (user.RequiresNicknameSetup)
            throw new InvalidOperationException("Complete nickname setup first.");

        var allowedAt = user.CreatedAt.Add(NewAccountCommentDelay);
        if (DateTime.UtcNow < allowedAt)
            throw new InvalidOperationException($"Comments are available at {allowedAt:yyyy-MM-dd HH:mm:ss} UTC.");

        var station = await unitOfWork.Stations.GetByIdAsync(request.StationId, cancellationToken)
            ?? throw new InvalidOperationException("Station not found.");

        if (!station.IsActive)
            throw new InvalidOperationException("Station is not active.");

        Fuel? fuel = null;
        if (request.FuelId is not null)
        {
            fuel = await unitOfWork.Fuels.GetByIdAsync(request.FuelId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Fuel not found.");

            var stationHasFuel = await unitOfWork.FuelPrices.Query()
                .AnyAsync(x => x.StationId == station.Id && x.FuelId == fuel.Id, cancellationToken);
            if (!stationHasFuel)
                throw new InvalidOperationException("Selected station does not have this fuel type.");
        }

        var content = NormalizeCommentContent(request.Content);
        var rating = NormalizeRequiredRating(request.Rating);

        var comment = new Comment
        {
            UserId = user.Id,
            StationId = station.Id,
            FuelId = fuel?.Id,
            Rating = rating,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        await unitOfWork.Comments.AddAsync(comment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        comment.User = user;
        comment.Station = station;
        comment.Fuel = fuel;
        return ToDto(comment);
    }

    public async Task<CommentDto?> UpdateAsync(int id, UpdateCommentRequest request, CancellationToken cancellationToken = default)
    {
        var comment = await unitOfWork.Comments.Query()
            .Include(x => x.User)
            .Include(x => x.Station)
            .Include(x => x.Fuel)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (comment is null)
            return null;

        return await UpdateLoadedCommentAsync(comment, request, cancellationToken);
    }

    public async Task<CommentDto?> UpdateOwnAsync(int userId, int id, UpdateCommentRequest request, CancellationToken cancellationToken = default)
    {
        var comment = await unitOfWork.Comments.Query()
            .Include(x => x.User)
            .Include(x => x.Station)
            .Include(x => x.Fuel)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (comment is null)
            return null;

        if (comment.UserId != userId)
            throw new UnauthorizedAccessException("You can edit only your own comments.");

        return await UpdateLoadedCommentAsync(comment, request, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var comment = await unitOfWork.Comments.GetByIdAsync(id, cancellationToken);
        if (comment is null)
            return false;

        unitOfWork.Comments.Remove(comment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CommentDto?> DeleteOwnAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        var comment = await unitOfWork.Comments.Query()
            .Include(x => x.User)
            .Include(x => x.Station)
            .Include(x => x.Fuel)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (comment is null)
            return null;

        if (comment.UserId != userId)
            throw new UnauthorizedAccessException("You can delete only your own comments.");

        var dto = ToDto(comment);
        unitOfWork.Comments.Remove(comment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return dto;
    }

    private async Task<CommentDto> UpdateLoadedCommentAsync(Comment comment, UpdateCommentRequest request, CancellationToken cancellationToken)
    {
        comment.Content = NormalizeCommentContent(request.Content);
        if (request.Rating is not null)
            comment.Rating = NormalizeRating(request.Rating.Value);

        comment.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(comment);
    }

    private static string NormalizeCommentContent(string content)
    {
        var normalized = (content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("Comment cannot be empty.");

        if (normalized.Length > 2000)
            throw new InvalidOperationException("Comment is too long.");

        return normalized;
    }

    private static int NormalizeRequiredRating(int? rating)
    {
        if (rating is null)
            throw new InvalidOperationException("Rating is required.");

        return NormalizeRating(rating.Value);
    }

    private static int NormalizeRating(int rating)
    {
        if (rating is < 1 or > 5)
            throw new InvalidOperationException("Rating must be from 1 to 5.");

        return rating;
    }

    private static CommentDto ToDto(Comment comment) =>
        new(
            comment.Id,
            comment.UserId,
            comment.StationId,
            comment.Station.Name,
            comment.FuelId,
            comment.Fuel?.Name,
            comment.User.IsDeleted ? "Deleted user" : MaskEmail(comment.User.Email),
            comment.Content,
            comment.Rating,
            comment.CreatedAt,
            comment.UpdatedAt);

    private static AdminCommentDto ToAdminDto(Comment comment)
    {
        var nickname = comment.User.Nickname ?? string.Empty;
        var authorName = !string.IsNullOrWhiteSpace(comment.User.DisplayName)
            ? comment.User.DisplayName!
            : !string.IsNullOrWhiteSpace(nickname)
                ? nickname
                : comment.User.Email;
        var authorType = comment.User.IsDeleted ? "deleted" : "registered";

        return new AdminCommentDto(
            comment.Id,
            comment.UserId,
            comment.User.Email,
            nickname,
            comment.User.IsDeleted ? "Deleted user" : authorName,
            authorType,
            comment.StationId,
            comment.Station.Name,
            comment.FuelId,
            comment.Fuel?.Name,
            comment.Content,
            comment.Rating,
            comment.CreatedAt,
            comment.UpdatedAt,
            "published");
    }

    private static string MaskEmail(string email)
    {
        var local = email.Split('@')[0];
        if (string.IsNullOrEmpty(local))
            return "user";

        if (local.Length <= 2)
            return $"{local[..1]}***";

        return $"{local[..2]}***";
    }
}

public sealed class StationAdminService(IUnitOfWork unitOfWork, IFuelNormalizer normalizer, IFuelDataService fuelDataService) : IStationAdminService
{
    public async Task<StationDto> CreateAsync(UpsertStationRequest request, CancellationToken cancellationToken = default)
    {
        var station = new Station
        {
            Name = request.Name.Trim(),
            NormalizedKey = normalizer.NormalizeStationKey(request.Name),
            Address = request.Address.Trim(),
            City = request.City.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            ImageUrl = NormalizeOptionalUrl(request.ImageUrl, "Image URL"),
            WebsiteUrl = NormalizeOptionalUrl(request.WebsiteUrl, "Website URL"),
            PhotoUrls = StationPhotoUrls.Serialize(NormalizePhotoUrls(request.PhotoUrls))
        };

        await unitOfWork.Stations.AddAsync(station, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await fuelDataService.GetStationAsync(station.Id, cancellationToken))!;
    }

    public async Task<StationDto?> UpdateAsync(int id, UpsertStationRequest request, CancellationToken cancellationToken = default)
    {
        var station = await unitOfWork.Stations.GetByIdAsync(id, cancellationToken);
        if (station is null) return null;

        station.Name = request.Name.Trim();
        station.NormalizedKey = normalizer.NormalizeStationKey(request.Name);
        station.Address = request.Address.Trim();
        station.City = request.City.Trim();
        station.Latitude = request.Latitude;
        station.Longitude = request.Longitude;
        station.ImageUrl = NormalizeOptionalUrl(request.ImageUrl, "Image URL");
        station.WebsiteUrl = NormalizeOptionalUrl(request.WebsiteUrl, "Website URL");
        station.PhotoUrls = StationPhotoUrls.Serialize(NormalizePhotoUrls(request.PhotoUrls));
        station.UpdatedAt = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await fuelDataService.GetStationAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var station = await unitOfWork.Stations.GetByIdAsync(id, cancellationToken);
        if (station is null) return false;

        station.IsActive = false;
        station.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string? NormalizeOptionalUrl(string? value, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException($"{fieldName} must be an absolute http or https URL.");

        return normalized;
    }

    private static IReadOnlyList<string> NormalizePhotoUrls(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
            return [];

        var normalized = values
            .SelectMany(value => (value ?? string.Empty)
                .Replace("\r", "\n", StringComparison.Ordinal)
                .Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        foreach (var value in normalized)
        {
            NormalizeOptionalUrl(value, "Station photo URL");
        }

        return normalized;
    }
}

internal static class StationPhotoUrls
{
    public static IReadOnlyList<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(value)?
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return value
                .Replace("\r", "\n", StringComparison.Ordinal)
                .Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public static string? Serialize(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? null : JsonSerializer.Serialize(values);
    }
}

public sealed class SubscriptionService(
    IUnitOfWork unitOfWork,
    ISubscriptionNotificationQueue notificationQueue) : ISubscriptionService
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly TimeSpan DefaultSendTime = new(9, 0, 0);

    public async Task<IReadOnlyList<SubscriptionDto>> ListAsync(int userId, CancellationToken cancellationToken = default)
    {
        var subscriptions = await unitOfWork.Subscriptions.Query()
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Fuel)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.City)
            .ThenBy(x => x.Fuel.SortOrder)
            .ToListAsync(cancellationToken);

        return subscriptions.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<SubscriptionDto>> CreateAsync(int userId, SubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var user = await ValidateUserAsync(userId, cancellationToken);
        var city = NormalizeCity(request.City);
        var email = ResolveConfirmedProfileEmail(user);
        var sendTime = ParseSendTime(request.SendTime);
        var fuelIds = NormalizeFuelIds(request);

        if (string.IsNullOrWhiteSpace(city))
            throw new InvalidOperationException("Місто є обов'язковим.");

        var existingFuelIds = await unitOfWork.Fuels.Query()
            .AsNoTracking()
            .Where(x => fuelIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (existingFuelIds.Count != fuelIds.Count)
            throw new InvalidOperationException("Один або кілька типів пального не знайдено.");

        var subscriptions = await unitOfWork.Subscriptions.Query()
            .Include(x => x.Fuel)
            .Include(x => x.User)
            .Where(x => x.UserId == userId && x.City == city && fuelIds.Contains(x.FuelId))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var fuelId in fuelIds)
        {
            var subscription = subscriptions.FirstOrDefault(x => x.FuelId == fuelId);
            if (subscription is null)
            {
                subscription = new Subscription
                {
                    UserId = userId,
                    FuelId = fuelId,
                    City = city,
                    Email = email,
                    Frequency = request.Frequency,
                    SendTime = sendTime,
                    IsActive = request.IsActive,
                    CreatedAt = now
                };

                await unitOfWork.Subscriptions.AddAsync(subscription, cancellationToken);
                subscriptions.Add(subscription);
                continue;
            }

            subscription.Email = email;
            subscription.Frequency = request.Frequency;
            subscription.SendTime = sendTime;
            subscription.IsActive = request.IsActive;
            subscription.UpdatedAt = now;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var subscription in subscriptions.Where(x => fuelIds.Contains(x.FuelId)))
            notificationQueue.QueueSubscriptionCreated(userId, subscription.Id);

        return await ListByIdsAsync(userId, subscriptions.Select(x => x.Id).ToList(), cancellationToken);
    }

    public async Task<SubscriptionDto?> UpdateAsync(int userId, int id, SubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var user = await ValidateUserAsync(userId, cancellationToken);
        var subscription = await unitOfWork.Subscriptions.Query()
            .Include(x => x.Fuel)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (subscription is null)
            return null;

        var fuelIds = NormalizeFuelIds(request);
        var fuelId = fuelIds[0];
        if (!await unitOfWork.Fuels.ExistsAsync(x => x.Id == fuelId, cancellationToken))
            throw new InvalidOperationException("Тип пального не знайдено.");

        var city = NormalizeCity(request.City);
        if (string.IsNullOrWhiteSpace(city))
            throw new InvalidOperationException("Місто є обов'язковим.");

        var duplicate = await unitOfWork.Subscriptions.ExistsAsync(
            x => x.Id != id && x.UserId == userId && x.FuelId == fuelId && x.City == city,
            cancellationToken);
        if (duplicate)
            throw new InvalidOperationException("Підписка з таким містом і типом пального вже існує.");

        subscription.FuelId = fuelId;
        subscription.City = city;
        subscription.Email = ResolveConfirmedProfileEmail(user);
        subscription.Frequency = request.Frequency;
        subscription.SendTime = ParseSendTime(request.SendTime);
        subscription.IsActive = request.IsActive;
        subscription.UpdatedAt = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await unitOfWork.Subscriptions.Query()
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Fuel)
            .FirstAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        return ToDto(updated);
    }

    public async Task<bool> DeleteAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        var subscription = await unitOfWork.Subscriptions.Query()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (subscription is null)
            return false;

        unitOfWork.Subscriptions.Remove(subscription);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<User> ValidateUserAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("Користувача не знайдено.");

        if (!user.EmailConfirmed)
            throw new InvalidOperationException("Підтвердьте email перед оформленням розсилки.");

        if (user.RequiresNicknameSetup)
            throw new InvalidOperationException("Спочатку завершіть налаштування нікнейму.");

        return user;
    }

    private async Task<IReadOnlyList<SubscriptionDto>> ListByIdsAsync(int userId, IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        var subscriptions = await unitOfWork.Subscriptions.Query()
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Fuel)
            .Where(x => x.UserId == userId && ids.Contains(x.Id))
            .OrderBy(x => x.Fuel.SortOrder)
            .ToListAsync(cancellationToken);

        return subscriptions.Select(ToDto).ToList();
    }

    private static IReadOnlyList<int> NormalizeFuelIds(SubscriptionRequest request)
    {
        var requestedFuelIds = request.FuelIds is { Count: > 0 }
            ? request.FuelIds
            : request.FuelId is null
                ? Array.Empty<int>()
                : [request.FuelId.Value];

        var fuelIds = requestedFuelIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        if (fuelIds.Count == 0)
            throw new InvalidOperationException("Оберіть хоча б один тип пального.");

        return fuelIds;
    }

    private static string NormalizeCity(string city) =>
        (city ?? string.Empty).Trim().ToLowerInvariant();

    private static string ResolveConfirmedProfileEmail(User user)
    {
        var email = user.Email.Trim().ToLowerInvariant();
        if (!EmailRegex.IsMatch(email))
            throw new InvalidOperationException("У профілі вказано некоректний email.");

        return email;
    }

    private static TimeSpan ParseSendTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DefaultSendTime;

        var normalized = value.Trim();
        if (TimeSpan.TryParse(normalized, out var parsed) && parsed >= TimeSpan.Zero && parsed < TimeSpan.FromDays(1))
            return new TimeSpan(parsed.Hours, parsed.Minutes, 0);

        throw new InvalidOperationException("Вкажіть коректний час відправки.");
    }

    private static string FormatSendTime(TimeSpan value) =>
        $"{value.Hours:00}:{value.Minutes:00}";

    private static SubscriptionDto ToDto(Subscription subscription) =>
        new(
            subscription.Id,
            subscription.FuelId,
            subscription.Fuel.Code,
            subscription.Fuel.Name,
            subscription.City,
            subscription.Frequency,
            FormatSendTime(subscription.SendTime),
            string.IsNullOrWhiteSpace(subscription.Email) ? subscription.User.Email : subscription.Email,
            subscription.IsActive,
            subscription.LastSentAt,
            subscription.CreatedAt,
            subscription.UpdatedAt);
}

public sealed class ApiTokenService(IUnitOfWork unitOfWork, ITokenHasher tokenHasher) : IApiTokenService
{
    public Task<ApiTokenCreatedDto> CreateAsync(int adminUserId, ApiTokenCreateRequest request, CancellationToken cancellationToken = default) =>
        CreateInternalAsync(adminUserId, request, cancellationToken);

    private async Task<ApiTokenCreatedDto> CreateInternalAsync(int userId, ApiTokenCreateRequest request, CancellationToken cancellationToken)
    {
        var name = NormalizeTokenDisplayName(request.Name);
        var normalizedName = NormalizeTokenComparisonName(name);
        var scopes = NormalizeScopes(request.Scopes);
        var now = DateTime.UtcNow;
        var activeNames = await unitOfWork.ApiTokens.Query()
            .AsNoTracking()
            .Where(x =>
                x.CreatedByUserId == userId &&
                x.RevokedAt == null &&
                (x.ExpiresAt == null || x.ExpiresAt > now))
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        if (activeNames.Any(x => NormalizeTokenComparisonName(x) == normalizedName))
            throw new InvalidOperationException("Інтеграція з такою назвою вже існує.");

        var rawToken = $"lfm_{CreateSecret()}";
        var token = new ApiToken
        {
            Name = name,
            Scopes = scopes,
            ExpiresAt = request.ExpiresAt,
            CreatedByUserId = userId,
            TokenHash = tokenHasher.Hash(rawToken)
        };

        await unitOfWork.ApiTokens.AddAsync(token, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ApiTokenCreatedDto(token.Id, token.Name, rawToken, token.Scopes, token.ExpiresAt);
    }

    public Task<PagedResult<ApiTokenDto>> ListAsync(ApiTokenQuery query, CancellationToken cancellationToken = default) =>
        ListInternalAsync(query, createdByUserId: null, cancellationToken);

    private async Task<PagedResult<ApiTokenDto>> ListInternalAsync(ApiTokenQuery query, int? createdByUserId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var tokensQuery = unitOfWork.ApiTokens.Query()
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
            .AsQueryable();

        if (createdByUserId is not null)
            tokensQuery = tokensQuery.Where(x => x.CreatedByUserId == createdByUserId.Value);

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            tokensQuery = tokensQuery.Where(x =>
                EF.Functions.Like(x.Name, pattern) ||
                EF.Functions.Like(x.Scopes, pattern) ||
                EF.Functions.Like(x.CreatedByUser.Email, pattern) ||
                (x.CreatedByUser.Nickname != null && EF.Functions.Like(x.CreatedByUser.Nickname, pattern)));
        }

        var scope = query.Scope?.Trim();
        if (!string.IsNullOrWhiteSpace(scope))
        {
            var pattern = $"%{scope}%";
            tokensQuery = tokensQuery.Where(x => EF.Functions.Like(x.Scopes, pattern));
        }

        var userEmail = query.UserEmail?.Trim();
        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            var pattern = $"%{userEmail}%";
            tokensQuery = tokensQuery.Where(x => EF.Functions.Like(x.CreatedByUser.Email, pattern));
        }

        var status = query.Status?.Trim().ToLowerInvariant();
        tokensQuery = status switch
        {
            "active" => tokensQuery.Where(x => x.RevokedAt == null && (x.ExpiresAt == null || x.ExpiresAt > now)),
            "revoked" => tokensQuery.Where(x => x.RevokedAt != null),
            "expired" => tokensQuery.Where(x => x.RevokedAt == null && x.ExpiresAt != null && x.ExpiresAt <= now),
            _ => tokensQuery
        };

        if (query.CreatedFrom is not null)
        {
            var createdFrom = query.CreatedFrom.Value.Date;
            tokensQuery = tokensQuery.Where(x => x.CreatedAt >= createdFrom);
        }

        if (query.CreatedTo is not null)
        {
            var createdToExclusive = query.CreatedTo.Value.Date.AddDays(1);
            tokensQuery = tokensQuery.Where(x => x.CreatedAt < createdToExclusive);
        }

        if (query.ExpiresFrom is not null)
        {
            var expiresFrom = query.ExpiresFrom.Value.Date;
            tokensQuery = tokensQuery.Where(x => x.ExpiresAt != null && x.ExpiresAt >= expiresFrom);
        }

        if (query.ExpiresTo is not null)
        {
            var expiresToExclusive = query.ExpiresTo.Value.Date.AddDays(1);
            tokensQuery = tokensQuery.Where(x => x.ExpiresAt != null && x.ExpiresAt < expiresToExclusive);
        }

        var totalCount = await tokensQuery.CountAsync(cancellationToken);
        var tokens = await tokensQuery
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ApiTokenDto>(
            tokens.Select(x => ToApiTokenDto(x, now)).ToList(),
            page,
            pageSize,
            totalCount);
    }

    public Task<bool> RevokeAsync(int id, CancellationToken cancellationToken = default) =>
        RevokeInternalAsync(id, createdByUserId: null, cancellationToken);

    private async Task<bool> RevokeInternalAsync(int id, int? createdByUserId, CancellationToken cancellationToken)
    {
        var tokenQuery = unitOfWork.ApiTokens.Query();
        if (createdByUserId is not null)
            tokenQuery = tokenQuery.Where(x => x.CreatedByUserId == createdByUserId.Value);

        var token = await tokenQuery.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (token is null) return false;
        token.RevokedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        DeleteInternalAsync(id, createdByUserId: null, cancellationToken);

    private async Task<bool> DeleteInternalAsync(int id, int? createdByUserId, CancellationToken cancellationToken)
    {
        var tokenQuery = unitOfWork.ApiTokens.Query();
        if (createdByUserId is not null)
            tokenQuery = tokenQuery.Where(x => x.CreatedByUserId == createdByUserId.Value);

        var token = await tokenQuery.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (token is null) return false;
        unitOfWork.ApiTokens.Remove(token);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ValidateAsync(string token, string requiredScope, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var candidates = await unitOfWork.ApiTokens.Query()
            .Include(x => x.CreatedByUser)
            .Where(x =>
                x.RevokedAt == null &&
                (x.ExpiresAt == null || x.ExpiresAt > now) &&
                !x.CreatedByUser.IsDeleted)
            .ToListAsync(cancellationToken);

        return candidates.Any(x =>
            HasScope(x.Scopes, requiredScope) &&
            tokenHasher.Verify(token, x.TokenHash));
    }

    private static bool HasScope(string scopes, string requiredScope)
    {
        var grantedScopes = scopes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .ToHashSet();

        var required = requiredScope.ToLowerInvariant();
        if (grantedScopes.Contains("*") || grantedScopes.Contains(required))
            return true;

        if (required.EndsWith(":read", StringComparison.Ordinal) && grantedScopes.Contains("read:all"))
            return true;

        if (required.EndsWith(":write", StringComparison.Ordinal) && grantedScopes.Contains("write:all"))
            return true;

        return false;
    }

    private static ApiTokenDto ToApiTokenDto(ApiToken token, DateTime now) =>
        new(
            token.Id,
            token.Name,
            token.Scopes,
            token.CreatedAt,
            token.ExpiresAt,
            token.RevokedAt,
            token.CreatedByUserId,
            token.CreatedByUser.Email,
            GetTokenStatus(token, now));

    private static string GetTokenStatus(ApiToken token, DateTime now)
    {
        if (token.RevokedAt is not null)
            return "revoked";

        if (token.ExpiresAt is not null && token.ExpiresAt <= now)
            return "expired";

        return "active";
    }

    private static string NormalizeScopes(string? value)
    {
        var scopes = string.IsNullOrWhiteSpace(value) ? "fuel:read" : value;
        var normalizedScopes = scopes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedScopes.Length == 0)
            normalizedScopes = ["fuel:read"];

        return string.Join(",", normalizedScopes);
    }

    private static string NormalizeTokenDisplayName(string value)
    {
        var normalized = Regex.Replace((value ?? string.Empty).Trim(), @"\s+", " ");
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("Token name is required.");

        if (normalized.Length > 100)
            throw new InvalidOperationException("Token name is too long.");

        return normalized;
    }

    private static string NormalizeTokenComparisonName(string value) =>
        Regex.Replace((value ?? string.Empty).Trim(), @"\s+", " ").ToUpperInvariant();

    private static string CreateSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace("+", string.Empty).Replace("/", string.Empty).TrimEnd('=');
    }
}

public sealed class UserAdminService(IUnitOfWork unitOfWork) : IUserAdminService
{
    public async Task<PagedResult<UserAdminDto>> ListAsync(UserAdminQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var usersQuery = unitOfWork.Users.Query().AsNoTracking();

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            var matchingRoles = Enum.GetValues<UserRole>()
                .Where(x => x.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            usersQuery = usersQuery.Where(x =>
                EF.Functions.Like(x.Email, pattern) ||
                (x.Nickname != null && EF.Functions.Like(x.Nickname, pattern)) ||
                (x.NormalizedNickname != null && EF.Functions.Like(x.NormalizedNickname, pattern)) ||
                (x.DisplayName != null && EF.Functions.Like(x.DisplayName, pattern)) ||
                EF.Functions.Like(x.AuthProvider, pattern) ||
                (matchingRoles.Length > 0 && matchingRoles.Contains(x.Role)));
        }

        if (query.Role is not null)
            usersQuery = usersQuery.Where(x => x.Role == query.Role.Value);

        var provider = NormalizeProvider(query.Provider);
        if (provider is not null)
            usersQuery = usersQuery.Where(x => x.AuthProvider == provider);

        var status = query.Status?.Trim().ToLowerInvariant();
        usersQuery = status switch
        {
            "active" => usersQuery.Where(x => !x.IsDeleted),
            "deleted" => usersQuery.Where(x => x.IsDeleted),
            "blocked" => usersQuery.Where(_ => false),
            _ => usersQuery
        };

        if (query.CreatedFrom is not null)
            usersQuery = usersQuery.Where(x => x.CreatedAt >= query.CreatedFrom.Value.Date);

        if (query.CreatedTo is not null)
        {
            var createdToExclusive = query.CreatedTo.Value.Date.AddDays(1);
            usersQuery = usersQuery.Where(x => x.CreatedAt < createdToExclusive);
        }

        var total = await usersQuery.CountAsync(cancellationToken);
        var users = await usersQuery
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UserAdminDto>(users.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<UserAdminDto?> UpdateRoleAsync(int userId, UserRole role, CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return null;

        if (user.Role != role)
        {
            user.Role = role;
            user.TokenVersion++;
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiresAt = null;
            user.RefreshTokenRevokedAt = DateTime.UtcNow;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    public async Task<bool> DeleteAsync(int userId, int currentAdminUserId, CancellationToken cancellationToken = default)
    {
        if (userId == currentAdminUserId)
            throw new InvalidOperationException("Admin cannot delete own account from admin panel.");

        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return false;

        var apiTokens = await unitOfWork.ApiTokens.Query()
            .Where(x => x.CreatedByUserId == user.Id)
            .ToListAsync(cancellationToken);

        foreach (var token in apiTokens)
            unitOfWork.ApiTokens.Remove(token);

        unitOfWork.Users.Remove(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string? NormalizeProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return null;

        return provider.Trim().ToLowerInvariant() switch
        {
            "local" => "Local",
            "google" => "Google",
            "deleted" => "Deleted",
            var value => value
        };
    }

    private static UserAdminDto ToDto(User user)
    {
        var status = user.IsDeleted ? "deleted" : "active";
        return new(
            user.Id,
            user.Email,
            user.Role.ToString(),
            user.EmailConfirmed,
            user.CreatedAt,
            user.LastLoginAt,
            string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName,
            string.IsNullOrWhiteSpace(user.Nickname) ? user.Email.Split('@')[0] : user.Nickname,
            user.AuthProvider,
            user.IsDeleted,
            user.DeletedAt,
            status);
    }
}

public sealed class DataSourceService(IUnitOfWork unitOfWork) : IDataSourceService
{
    public async Task<IReadOnlyList<DataSourceDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await unitOfWork.DataSources.Query()
            .OrderBy(x => x.Name)
            .Select(x => new DataSourceDto(x.Id, x.Name, x.Url, x.Type, x.Enabled, x.LastSuccessAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<DataSourceDto> CreateAsync(UpsertDataSourceRequest request, CancellationToken cancellationToken = default)
    {
        var source = new DataSource { Name = request.Name.Trim(), Url = request.Url.Trim(), Type = request.Type, Enabled = request.Enabled };
        await unitOfWork.DataSources.AddAsync(source, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DataSourceDto(source.Id, source.Name, source.Url, source.Type, source.Enabled, source.LastSuccessAt);
    }

    public async Task<DataSourceDto?> UpdateAsync(int id, UpsertDataSourceRequest request, CancellationToken cancellationToken = default)
    {
        var source = await unitOfWork.DataSources.GetByIdAsync(id, cancellationToken);
        if (source is null) return null;
        source.Name = request.Name.Trim();
        source.Url = request.Url.Trim();
        source.Type = request.Type;
        source.Enabled = request.Enabled;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DataSourceDto(source.Id, source.Name, source.Url, source.Type, source.Enabled, source.LastSuccessAt);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var source = await unitOfWork.DataSources.GetByIdAsync(id, cancellationToken);
        if (source is null) return false;
        unitOfWork.DataSources.Remove(source);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ParserRunDto>> GetParserRunsAsync(CancellationToken cancellationToken = default)
    {
        return await unitOfWork.ParserRuns.Query()
            .Include(x => x.Source)
            .OrderByDescending(x => x.StartedAt)
            .Take(100)
            .Select(x => new ParserRunDto(x.Id, x.SourceId, x.Source.Name, x.StartedAt, x.FinishedAt, x.Status.ToString(), x.RecordsFound, x.RecordsSaved, x.Error))
            .ToListAsync(cancellationToken);
    }
}

public sealed class FuelPriceImportService(
    IUnitOfWork unitOfWork,
    IFuelNormalizer normalizer,
    IPriceChangeDetector priceChangeDetector,
    ILogger<FuelPriceImportService> logger) : IFuelPriceImportService
{
    public async Task<FuelImportResult> ImportAsync(int sourceId, IReadOnlyList<FuelPriceRecord> records, CancellationToken cancellationToken = default)
    {
        var saved = 0;

        foreach (var record in records)
        {
            var fuelCode = normalizer.NormalizeFuelCode(record.FuelName);
            var fuel = await unitOfWork.Fuels.Query().FirstOrDefaultAsync(x => x.Code == fuelCode, cancellationToken);
            if (fuel is null)
            {
                logger.LogWarning("Unknown fuel type from parser: {FuelName}", record.FuelName);
                continue;
            }

            var stationKey = normalizer.NormalizeStationKey(record.StationName);
            var city = string.IsNullOrWhiteSpace(record.City) ? "Харків" : record.City.Trim();
            var station = await unitOfWork.Stations.Query()
                .FirstOrDefaultAsync(x => x.NormalizedKey == stationKey && x.City == city, cancellationToken);

            if (station is null)
            {
                station = new Station
                {
                    Name = record.StationName.Trim(),
                    NormalizedKey = stationKey,
                    City = city,
                    Address = string.IsNullOrWhiteSpace(record.Address) ? "-" : record.Address.Trim(),
                    Latitude = record.Latitude ?? 0,
                    Longitude = record.Longitude ?? 0,
                    ImageUrl = "default-station.jpg"
                };
                await unitOfWork.Stations.AddAsync(station, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var date = record.Date.Date;
            var existing = await unitOfWork.FuelPrices.Query()
                .FirstOrDefaultAsync(x => x.StationId == station.Id && x.FuelId == fuel.Id && x.Date == date, cancellationToken);

            if (existing is not null)
                continue;

            var latestPrice = await unitOfWork.FuelPrices.Query()
                .Where(x => x.StationId == station.Id && x.FuelId == fuel.Id)
                .OrderByDescending(x => x.Date)
                .Select(x => (decimal?)x.Price)
                .FirstOrDefaultAsync(cancellationToken);

            if (!priceChangeDetector.IsMeaningfulChange(latestPrice, record.Price))
                continue;

            await unitOfWork.FuelPrices.AddAsync(new FuelPrice
            {
                StationId = station.Id,
                FuelId = fuel.Id,
                SourceId = sourceId,
                Date = date,
                Price = record.Price
            }, cancellationToken);

            saved++;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new FuelImportResult(records.Count, saved);
    }
}
