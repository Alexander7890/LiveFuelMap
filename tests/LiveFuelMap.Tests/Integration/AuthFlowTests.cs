using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.DAL.Entities;
using LiveFuelMap.DAL.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LiveFuelMap.Tests.Integration;

public sealed class AuthFlowTests(LiveFuelMapApiFactory factory) : IClassFixture<LiveFuelMapApiFactory>
{
    private readonly LiveFuelMapApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_ReturnsJwtAndRefreshToken()
    {
        var auth = await RegisterAndLoginAsync("jwt-login@example.com");

        Assert.False(string.IsNullOrWhiteSpace(auth.GetProperty("token").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(auth.GetProperty("refreshToken").GetString()));
        Assert.Equal("Bearer", auth.GetProperty("tokenType").GetString());
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsStructuredUkrainianError()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "missing-login@example.com",
            password = "password123"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("Користувача з таким email не знайдено.", json.RootElement.GetProperty("message").GetString());
        Assert.Equal(
            "Користувача з таким email не знайдено.",
            json.RootElement.GetProperty("errors").GetProperty("email")[0].GetString());
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsStructuredUkrainianError()
    {
        await RegisterAndLoginAsync("wrong-password-login@example.com");

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "wrong-password-login@example.com",
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("Невірний пароль.", json.RootElement.GetProperty("message").GetString());
        Assert.Equal("Невірний пароль.", json.RootElement.GetProperty("errors").GetProperty("password")[0].GetString());
    }

    [Fact]
    public async Task Verify_WithJwt_ReturnsCurrentUser()
    {
        var auth = await RegisterAndLoginAsync("jwt-verify@example.com");

        UseBearer(auth.GetProperty("token").GetString()!);
        var response = await _client.GetAsync("/api/auth/verify");

        response.EnsureSuccessStatusCode();
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("jwt-verify@example.com", json.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Refresh_WithRefreshToken_ReturnsNewTokens()
    {
        var auth = await RegisterAndLoginAsync("jwt-refresh@example.com");
        var refreshToken = auth.GetProperty("refreshToken").GetString();

        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });

        response.EnsureSuccessStatusCode();
        var refreshed = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.False(string.IsNullOrWhiteSpace(refreshed.RootElement.GetProperty("token").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(refreshed.RootElement.GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task Refresh_WithWrongToken_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLogin_NewUser_CreatesUserAndReturnsWorkingJwt()
    {
        AuthResultDto auth;
        using (var scope = _factory.Services.CreateScope())
        {
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            auth = await authService.LoginWithGoogleAsync(new GoogleAccountDto(
                "google-new-user@example.com",
                "Google User",
                "https://example.com/google-avatar.png",
                "google-provider-user-id",
                true));
        }

        Assert.True(auth.UserId > 0);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.True(auth.RequiresNickname);
        Assert.Equal("google-new-user", auth.SuggestedNickname);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
            var user = db.Users.Single(x => x.Email == "google-new-user@example.com");
            Assert.Equal(auth.UserId, user.Id);
            Assert.Equal("Google", user.AuthProvider);
            Assert.Equal("google-provider-user-id", user.ExternalProviderId);
            Assert.Equal("Google User", user.GoogleName);
            Assert.Null(user.DisplayName);
            Assert.Null(user.Nickname);
            Assert.Null(user.NormalizedNickname);
            Assert.True(user.RequiresNicknameSetup);
            Assert.True(user.EmailConfirmed);
        }

        UseBearer(auth.Token);
        var response = await _client.GetAsync("/api/auth/verify");

        response.EnsureSuccessStatusCode();
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(auth.UserId, json.RootElement.GetProperty("userId").GetInt32());
        Assert.Equal("google-new-user@example.com", json.RootElement.GetProperty("email").GetString());
        Assert.True(json.RootElement.GetProperty("requiresNicknameSetup").GetBoolean());

        AuthResultDto repeatAuth;
        using (var scope = _factory.Services.CreateScope())
        {
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            repeatAuth = await authService.LoginWithGoogleAsync(new GoogleAccountDto(
                "google-new-user@example.com",
                "Google User",
                "https://example.com/google-avatar.png",
                "google-provider-user-id",
                true));
        }

        Assert.Equal(auth.UserId, repeatAuth.UserId);
        Assert.True(repeatAuth.RequiresNickname);
        Assert.Equal("google-new-user", repeatAuth.SuggestedNickname);

        var setupResponse = await _client.PostAsJsonAsync("/api/profile/setup-nickname", new
        {
            nickname = "Cool_Користувач-1"
        });
        setupResponse.EnsureSuccessStatusCode();
        var setupJson = await JsonDocument.ParseAsync(await setupResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Cool_Користувач-1", setupJson.RootElement.GetProperty("nickname").GetString());
        Assert.False(setupJson.RootElement.GetProperty("requiresNicknameSetup").GetBoolean());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
            var user = db.Users.Single(x => x.Email == "google-new-user@example.com");
            Assert.Equal("Cool_Користувач-1", user.Nickname);
            Assert.Equal("cool_користувач-1", user.NormalizedNickname);
            Assert.False(user.RequiresNicknameSetup);
            Assert.Equal(1, db.Users.Count(x => x.Email == "google-new-user@example.com"));
        }

        AuthResultDto completedRepeatAuth;
        using (var scope = _factory.Services.CreateScope())
        {
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            completedRepeatAuth = await authService.LoginWithGoogleAsync(new GoogleAccountDto(
                "google-new-user@example.com",
                "Google User",
                "https://example.com/google-avatar.png",
                "google-provider-user-id",
                true));
        }

        Assert.False(completedRepeatAuth.RequiresNickname);
        Assert.Null(completedRepeatAuth.SuggestedNickname);
    }

    [Fact]
    public async Task SetupNickname_WithTakenNickname_ReturnsBadRequest()
    {
        var takenNickname = $"taken{Guid.NewGuid():N}"[..16];
        var first = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"taken-{Guid.NewGuid():N}@example.com",
            password = "password123",
            confirmPassword = "password123",
            displayName = "Taken User",
            nickname = takenNickname
        });
        first.EnsureSuccessStatusCode();

        AuthResultDto auth;
        using (var scope = _factory.Services.CreateScope())
        {
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            auth = await authService.LoginWithGoogleAsync(new GoogleAccountDto(
                $"google-{Guid.NewGuid():N}@example.com",
                "Google Duplicate",
                null,
                $"google-provider-{Guid.NewGuid():N}",
                true));
        }

        UseBearer(auth.Token);
        var setupResponse = await _client.PostAsJsonAsync("/api/profile/setup-nickname", new
        {
            nickname = takenNickname.ToUpperInvariant()
        });

        Assert.Equal(HttpStatusCode.BadRequest, setupResponse.StatusCode);
        var json = await JsonDocument.ParseAsync(await setupResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Цей нікнейм уже використовується.", json.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Logout_RevokesCurrentAccessToken()
    {
        var auth = await RegisterAndLoginAsync("jwt-logout@example.com");
        UseBearer(auth.GetProperty("token").GetString()!);

        var logoutResponse = await _client.PostAsJsonAsync("/api/auth/logout", new
        {
            refreshToken = auth.GetProperty("refreshToken").GetString()
        });
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var verifyResponse = await _client.GetAsync("/api/auth/verify");
        Assert.Equal(HttpStatusCode.Unauthorized, verifyResponse.StatusCode);
    }

    [Fact]
    public async Task Profile_Update_ReturnsUpdatedPersonalData()
    {
        var auth = await RegisterAndLoginAsync("profile-update@example.com");
        UseBearer(auth.GetProperty("token").GetString()!);

        var response = await _client.PutAsJsonAsync("/api/profile", new
        {
            displayName = "Profile User",
            profileImageUrl = "https://example.com/avatar.png"
        });

        response.EnsureSuccessStatusCode();
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("Profile User", json.RootElement.GetProperty("displayName").GetString());
        Assert.Equal("https://example.com/avatar.png", json.RootElement.GetProperty("profileImageUrl").GetString());
        Assert.False(json.RootElement.GetProperty("emailConfirmed").GetBoolean());
    }

    [Fact]
    public async Task Profile_DeleteLocal_WithEmailAndPassword_SoftDeletesAccountAndRevokesAccess()
    {
        var email = "profile-delete-local@example.com";
        var auth = await RegisterAndLoginAsync(email);
        var userId = GetUserId(email);
        UseBearer(auth.GetProperty("token").GetString()!);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
            db.ApiTokens.Add(new ApiToken
            {
                Name = "owned-token",
                Scopes = "fuel:read",
                TokenHash = $"hash-{Guid.NewGuid():N}",
                CreatedByUserId = userId
            });
            db.Subscriptions.Add(new Subscription
            {
                UserId = userId,
                FuelId = 2,
                City = "kharkiv"
            });
            db.ChatMessages.Add(new ChatMessage
            {
                UserId = userId,
                SessionId = Guid.NewGuid().ToString("N"),
                UserMessage = "question",
                BotResponse = "answer",
                Intent = "fuel",
                Status = "Completed"
            });
            await db.SaveChangesAsync();
        }

        var deleteResponse = await DeleteProfileAsync(new
        {
            email,
            password = "password123"
        });

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
            var user = db.Users.Single(x => x.Id == userId);
            Assert.True(user.IsDeleted);
            Assert.NotNull(user.DeletedAt);
            Assert.Equal("Deleted", user.AuthProvider);
            Assert.NotEqual(email, user.Email);
            Assert.Null(user.RefreshTokenHash);
            Assert.Null(user.RefreshTokenExpiresAt);
            Assert.NotNull(user.RefreshTokenRevokedAt);
            Assert.False(db.Subscriptions.Any(x => x.UserId == userId));
            Assert.All(db.ApiTokens.Where(x => x.CreatedByUserId == userId), token => Assert.NotNull(token.RevokedAt));
            Assert.Null(db.ChatMessages.Single(x => x.UserMessage == "question").UserId);
        }

        var verifyResponse = await _client.GetAsync("/api/auth/verify");
        Assert.Equal(HttpStatusCode.Unauthorized, verifyResponse.StatusCode);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "password123"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Profile_DeleteLocal_WithWrongCredentials_ReturnsBadRequest()
    {
        var email = "profile-delete-wrong@example.com";
        var auth = await RegisterAndLoginAsync(email);
        var userId = GetUserId(email);
        UseBearer(auth.GetProperty("token").GetString()!);

        var wrongEmail = await DeleteProfileAsync(new
        {
            email = "someone-else@example.com",
            password = "password123"
        });

        Assert.Equal(HttpStatusCode.BadRequest, wrongEmail.StatusCode);

        var wrongPassword = await DeleteProfileAsync(new
        {
            email,
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.BadRequest, wrongPassword.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
        Assert.False(db.Users.Single(x => x.Id == userId).IsDeleted);
    }

    [Fact]
    public async Task Profile_DeleteGoogle_WithEmailCode_SoftDeletesAccount()
    {
        AuthResultDto auth;
        using (var scope = _factory.Services.CreateScope())
        {
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            auth = await authService.LoginWithGoogleAsync(new GoogleAccountDto(
                "profile-delete-google@example.com",
                "Google Delete",
                null,
                "google-delete-provider-id",
                true));
        }

        UseBearer(auth.Token);

        var requestCodeResponse = await _client.PostAsJsonAsync("/api/profile/delete-code", new
        {
            email = "profile-delete-google@example.com"
        });

        Assert.Equal(HttpStatusCode.NoContent, requestCodeResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
            var tokenHasher = scope.ServiceProvider.GetRequiredService<ITokenHasher>();
            var user = db.Users.Single(x => x.Id == auth.UserId);
            user.AccountDeletionTokenHash = tokenHasher.Hash("123456");
            user.AccountDeletionTokenExpiresAt = DateTime.UtcNow.AddMinutes(5);
            await db.SaveChangesAsync();
        }

        var deleteResponse = await DeleteProfileAsync(new
        {
            email = "profile-delete-google@example.com",
            verificationCode = "123456"
        });

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
            var user = db.Users.Single(x => x.Id == auth.UserId);
            Assert.True(user.IsDeleted);
            Assert.NotNull(user.DeletedAt);
            Assert.Equal("Deleted", user.AuthProvider);
            Assert.Null(user.ExternalProviderId);
            Assert.Null(user.AccountDeletionTokenHash);
        }

        var verifyResponse = await _client.GetAsync("/api/auth/verify");
        Assert.Equal(HttpStatusCode.Unauthorized, verifyResponse.StatusCode);
    }

    [Fact]
    public async Task CommentAuthor_CanEditAndDeleteOwnComment()
    {
        var email = "comment-owner@example.com";
        var auth = await RegisterAndLoginAsync(email);
        var commentId = await SeedCommentAsync(email, "original owner comment");
        UseBearer(auth.GetProperty("token").GetString()!);

        var updateResponse = await _client.PutAsJsonAsync($"/api/comments/{commentId}", new UpdateCommentRequest("updated by owner", 3));

        updateResponse.EnsureSuccessStatusCode();
        var json = await JsonDocument.ParseAsync(await updateResponse.Content.ReadAsStreamAsync());
        Assert.Equal("updated by owner", json.RootElement.GetProperty("content").GetString());
        Assert.Equal(3, json.RootElement.GetProperty("rating").GetInt32());
        Assert.Equal(GetUserId(email), json.RootElement.GetProperty("userId").GetInt32());
        Assert.Equal(JsonValueKind.String, json.RootElement.GetProperty("updatedAt").ValueKind);

        var deleteResponse = await _client.DeleteAsync($"/api/comments/{commentId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
        Assert.False(db.Comments.Any(x => x.Id == commentId));
    }

    [Fact]
    public async Task CommentAuthoring_RejectsEditingAnotherUsersComment()
    {
        var ownerEmail = "comment-real-owner@example.com";
        await RegisterAndLoginAsync(ownerEmail);
        var commentId = await SeedCommentAsync(ownerEmail, "do not touch");

        var otherAuth = await RegisterAndLoginAsync("comment-other-user@example.com");
        UseBearer(otherAuth.GetProperty("token").GetString()!);

        var updateResponse = await _client.PutAsJsonAsync($"/api/comments/{commentId}", new UpdateCommentRequest("hacked"));
        var deleteResponse = await _client.DeleteAsync($"/api/comments/{commentId}");

        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
        Assert.Equal("do not touch", db.Comments.Single(x => x.Id == commentId).Content);
    }

    [Fact]
    public async Task CreateSubscription_WithUnverifiedEmail_ReturnsBadRequest()
    {
        var auth = await RegisterAndLoginAsync("subscription-unverified@example.com");
        UseBearer(auth.GetProperty("token").GetString()!);

        var response = await _client.PostAsJsonAsync("/api/subscriptions", new
        {
            fuelId = 2,
            city = "kharkiv",
            frequency = "Daily"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSubscription_WithVerifiedEmail_CreatesSubscription()
    {
        var email = "subscription-verified@example.com";
        var auth = await RegisterAndLoginAsync(email);
        UseBearer(auth.GetProperty("token").GetString()!);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
            var user = db.Users.Single(x => x.Email == email);
            user.EmailConfirmed = true;
            user.EmailConfirmationTokenHash = null;
            user.EmailConfirmationTokenExpiresAt = null;
            await db.SaveChangesAsync();
        }

        var createResponse = await _client.PostAsJsonAsync("/api/subscriptions", new
        {
            fuelId = 2,
            city = "kharkiv",
            frequency = "Daily"
        });
        createResponse.EnsureSuccessStatusCode();

        var list = await _client.GetFromJsonAsync<JsonElement[]>("/api/subscriptions");
        Assert.NotNull(list);
        Assert.Contains(list!, item => item.GetProperty("fuelId").GetInt32() == 2);
    }

    [Fact]
    public async Task CreateAndUpdateSubscription_SavesScheduleAndStatus()
    {
        var email = "subscription-schedule@example.com";
        var auth = await RegisterAndLoginAsync(email);
        UseBearer(auth.GetProperty("token").GetString()!);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
            var user = db.Users.Single(x => x.Email == email);
            user.EmailConfirmed = true;
            user.EmailConfirmationTokenHash = null;
            user.EmailConfirmationTokenExpiresAt = null;
            await db.SaveChangesAsync();
        }

        var createResponse = await _client.PostAsJsonAsync("/api/subscriptions", new
        {
            fuelIds = new[] { 2, 3 },
            city = "kharkiv",
            frequency = "Weekly",
            sendTime = "15:45",
            isActive = true
        });
        createResponse.EnsureSuccessStatusCode();

        var list = await _client.GetFromJsonAsync<JsonElement[]>("/api/subscriptions");
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count(x => x.GetProperty("email").GetString() == email));
        Assert.All(list!.Where(x => x.GetProperty("email").GetString() == email), item =>
        {
            Assert.Equal("Weekly", item.GetProperty("frequency").GetString());
            Assert.Equal("15:45", item.GetProperty("sendTime").GetString());
            Assert.True(item.GetProperty("isActive").GetBoolean());
        });

        var subscriptionId = list!.First(x => x.GetProperty("fuelId").GetInt32() == 2).GetProperty("id").GetInt32();
        var updateResponse = await _client.PutAsJsonAsync($"/api/subscriptions/{subscriptionId}", new
        {
            fuelId = 2,
            city = "kharkiv",
            frequency = "Daily",
            sendTime = "08:30",
            isActive = false
        });
        updateResponse.EnsureSuccessStatusCode();

        var updatedList = await _client.GetFromJsonAsync<JsonElement[]>("/api/subscriptions");
        var updated = updatedList!.Single(x => x.GetProperty("id").GetInt32() == subscriptionId);
        Assert.Equal("Daily", updated.GetProperty("frequency").GetString());
        Assert.Equal("08:30", updated.GetProperty("sendTime").GetString());
        Assert.Equal(email, updated.GetProperty("email").GetString());
        Assert.False(updated.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Register_WithTakenNickname_ReturnsSuggestions()
    {
        var password = "password123";
        var nickname = $"taken{Guid.NewGuid():N}"[..16];
        var first = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"first-{Guid.NewGuid():N}@example.com",
            password,
            confirmPassword = password,
            displayName = "First User",
            nickname
        });
        first.EnsureSuccessStatusCode();

        var second = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"second-{Guid.NewGuid():N}@example.com",
            password,
            confirmPassword = password,
            displayName = "Second User",
            nickname
        });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var json = await JsonDocument.ParseAsync(await second.Content.ReadAsStreamAsync());
        Assert.True(json.RootElement.GetProperty("suggestions").GetArrayLength() > 0);
        Assert.True(json.RootElement.GetProperty("suggestions").GetArrayLength() <= 3);
    }

    private async Task<JsonElement> RegisterAndLoginAsync(string email)
    {
        var password = "password123";
        var nickname = email.Split('@')[0].Replace(".", "-");
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            confirmPassword = password,
            displayName = "Test User",
            nickname
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password
        });
        loginResponse.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await loginResponse.Content.ReadAsStreamAsync());
        return doc.RootElement.Clone();
    }

    private void UseBearer(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private Task<HttpResponseMessage> DeleteProfileAsync(object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/profile")
        {
            Content = JsonContent.Create(body)
        };

        return _client.SendAsync(request);
    }

    private int GetUserId(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
        return db.Users.Single(x => x.Email == email).Id;
    }

    private async Task<int> SeedCommentAsync(string email, string content)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
        var user = db.Users.Single(x => x.Email == email);
        var comment = new Comment
        {
            UserId = user.Id,
            StationId = 1,
            Content = content,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        db.Comments.Add(comment);
        await db.SaveChangesAsync();
        return comment.Id;
    }
}
