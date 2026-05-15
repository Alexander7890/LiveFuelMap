using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LiveFuelMap.BLL.DTOs;
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
