using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LiveFuelMap.DAL.Entities;
using LiveFuelMap.DAL.Enums;
using LiveFuelMap.DAL.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LiveFuelMap.Tests.Integration;

public sealed class AdminSecurityTests(LiveFuelMapApiFactory factory) : IClassFixture<LiveFuelMapApiFactory>
{
    private readonly LiveFuelMapApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task AdminApiTokens_RequireAdminRole()
    {
        var token = await RegisterAndLoginAsync("admin-denied@example.com");

        UseBearer(token);
        var response = await _client.GetAsync("/api/admin/api-tokens");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminUsers_ListSupportsSearchFiltersAndPagination()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var adminJwt = await CreateAdminAndLoginAsync($"uadm-{suffix}@example.com");
        var localEmail = $"alpha-{suffix}@example.com";
        var googleEmail = $"google-{suffix}@example.com";
        var deletedEmail = $"deleted-{suffix}@example.com";
        await RegisterAsync(localEmail);
        await RegisterAsync(googleEmail);
        await RegisterAsync(deletedEmail);
        await SetRoleAsync(googleEmail, UserRole.Admin);
        await SetProviderAsync(googleEmail, "Google", $"google-{Guid.NewGuid():N}");
        await MarkDeletedAsync(deletedEmail);

        UseBearer(adminJwt);
        var filteredResponse = await _client.GetAsync($"/api/admin/users?search=google&role=Admin&provider=Google&status=active&page=1&pageSize=10");
        filteredResponse.EnsureSuccessStatusCode();
        using var filteredDoc = await JsonDocument.ParseAsync(await filteredResponse.Content.ReadAsStreamAsync());
        var filteredRoot = filteredDoc.RootElement;
        var filteredItems = filteredRoot.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(1, filteredRoot.GetProperty("totalCount").GetInt32());
        var filteredUser = Assert.Single(filteredItems);
        Assert.Equal(googleEmail, filteredUser.GetProperty("email").GetString());
        Assert.Equal("Admin", filteredUser.GetProperty("role").GetString());
        Assert.Equal("Google", filteredUser.GetProperty("authProvider").GetString());
        Assert.Equal("active", filteredUser.GetProperty("status").GetString());

        var deletedResponse = await _client.GetAsync($"/api/admin/users?search=deleted&status=deleted&page=1&pageSize=1");
        deletedResponse.EnsureSuccessStatusCode();
        using var deletedDoc = await JsonDocument.ParseAsync(await deletedResponse.Content.ReadAsStreamAsync());
        var deletedRoot = deletedDoc.RootElement;
        var deletedUser = Assert.Single(deletedRoot.GetProperty("items").EnumerateArray());

        Assert.Equal(1, deletedRoot.GetProperty("page").GetInt32());
        Assert.Equal(1, deletedRoot.GetProperty("pageSize").GetInt32());
        Assert.Equal(deletedEmail, deletedUser.GetProperty("email").GetString());
        Assert.True(deletedUser.GetProperty("isDeleted").GetBoolean());
        Assert.Equal("deleted", deletedUser.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ExternalCurrentPrices_RejectsUserJwt()
    {
        var token = await RegisterAndLoginAsync("external-jwt@example.com");

        UseBearer(token);
        var response = await _client.GetAsync("/api/external/current-prices");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminCanCreateApiToken_AndUseItForExternalEndpoint()
    {
        var email = "api-token-admin@example.com";
        await RegisterAsync(email);
        await SetRoleAsync(email, UserRole.Admin);
        var adminJwt = await LoginAsync(email);

        UseBearer(adminJwt);
        var apiToken = await CreateApiTokenAsync("integration-test", "fuel:read");

        UseBearer(apiToken);
        var externalResponse = await _client.GetAsync("/api/external/current-prices?city=Kharkiv");

        externalResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ReadAllApiToken_CanReadComments()
    {
        var adminJwt = await CreateAdminAndLoginAsync("api-token-read-all@example.com");
        UseBearer(adminJwt);
        var apiToken = await CreateApiTokenAsync("read-all-test", "read:all");

        UseBearer(apiToken);
        var response = await _client.GetAsync("/api/external/comments?take=5");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task FuelReadApiToken_CannotUseWriteCheck()
    {
        var adminJwt = await CreateAdminAndLoginAsync("api-token-no-write@example.com");
        UseBearer(adminJwt);
        var apiToken = await CreateApiTokenAsync("read-only-test", "fuel:read");

        UseBearer(apiToken);
        var response = await _client.GetAsync("/api/external/write-check");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FuelWriteApiToken_CanUseWriteCheck()
    {
        var adminJwt = await CreateAdminAndLoginAsync("api-token-write@example.com");
        UseBearer(adminJwt);
        var apiToken = await CreateApiTokenAsync("write-test", "fuel:write");

        UseBearer(apiToken);
        var response = await _client.GetAsync("/api/external/write-check");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RevokedApiToken_CannotUseExternalEndpoint()
    {
        var adminJwt = await CreateAdminAndLoginAsync("api-token-revoke@example.com");
        UseBearer(adminJwt);
        var apiToken = await CreateApiTokenAsync("revoke-test", "fuel:read");
        var tokenId = await GetApiTokenIdAsync("revoke-test");

        UseBearer(apiToken);
        var allowedResponse = await _client.GetAsync("/api/external/current-prices?city=Kharkiv");
        allowedResponse.EnsureSuccessStatusCode();

        UseBearer(adminJwt);
        var revokeResponse = await _client.DeleteAsync($"/api/admin/api-tokens/{tokenId}");
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        UseBearer(apiToken);
        var rejectedResponse = await _client.GetAsync("/api/external/current-prices?city=Kharkiv");
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedResponse.StatusCode);
    }

    [Fact]
    public async Task AdminCanPermanentlyDeleteApiToken()
    {
        var adminJwt = await CreateAdminAndLoginAsync("api-token-delete@example.com");
        UseBearer(adminJwt);
        await CreateApiTokenAsync("delete-test", "fuel:read");
        var tokenId = await GetApiTokenIdAsync("delete-test");

        var deleteResponse = await _client.DeleteAsync($"/api/admin/api-tokens/{tokenId}/permanent");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await _client.GetAsync("/api/admin/api-tokens");
        listResponse.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await listResponse.Content.ReadAsStreamAsync());
        Assert.DoesNotContain(doc.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == tokenId);
    }

    [Fact]
    public async Task AdminApiTokens_ListSupportsSearchFiltersAndPagination()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var adminEmail = $"tf-{suffix}@example.com";
        var adminJwt = await CreateAdminAndLoginAsync(adminEmail);
        UseBearer(adminJwt);
        await CreateApiTokenAsync($"Primary API {suffix}", "fuel:read");
        await CreateApiTokenAsync($"Secondary API {suffix}", "comments:read");
        var revokedTokenId = await GetApiTokenIdAsync($"Secondary API {suffix}");
        var revokeResponse = await _client.DeleteAsync($"/api/admin/api-tokens/{revokedTokenId}");
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var expiredResponse = await _client.PostAsJsonAsync("/api/admin/api-tokens", new
        {
            name = $"Expired API {suffix}",
            scopes = "fuel:read",
            expiresAt = DateTime.UtcNow.AddDays(-1)
        });
        expiredResponse.EnsureSuccessStatusCode();

        var activeResponse = await _client.GetAsync($"/api/admin/api-tokens?search=Primary&scope=fuel:read&userEmail={Uri.EscapeDataString(adminEmail)}&status=active&page=1&pageSize=1");
        activeResponse.EnsureSuccessStatusCode();
        using var activeDoc = await JsonDocument.ParseAsync(await activeResponse.Content.ReadAsStreamAsync());
        var activeRoot = activeDoc.RootElement;
        var activeToken = Assert.Single(activeRoot.GetProperty("items").EnumerateArray());

        Assert.Equal(1, activeRoot.GetProperty("page").GetInt32());
        Assert.Equal(1, activeRoot.GetProperty("pageSize").GetInt32());
        Assert.Equal($"Primary API {suffix}", activeToken.GetProperty("name").GetString());
        Assert.Equal("active", activeToken.GetProperty("status").GetString());
        Assert.Equal(adminEmail, activeToken.GetProperty("createdByEmail").GetString());

        var revokedResponse = await _client.GetAsync($"/api/admin/api-tokens?search={Uri.EscapeDataString($"Secondary API {suffix}")}&userEmail={Uri.EscapeDataString(adminEmail)}&status=revoked&page=1&pageSize=10");
        revokedResponse.EnsureSuccessStatusCode();
        using var revokedDoc = await JsonDocument.ParseAsync(await revokedResponse.Content.ReadAsStreamAsync());
        var revokedToken = Assert.Single(revokedDoc.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("revoked", revokedToken.GetProperty("status").GetString());

        var expiredListResponse = await _client.GetAsync($"/api/admin/api-tokens?search={Uri.EscapeDataString($"Expired API {suffix}")}&userEmail={Uri.EscapeDataString(adminEmail)}&status=expired&page=1&pageSize=10");
        expiredListResponse.EnsureSuccessStatusCode();
        using var expiredDoc = await JsonDocument.ParseAsync(await expiredListResponse.Content.ReadAsStreamAsync());
        var expiredToken = Assert.Single(expiredDoc.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("expired", expiredToken.GetProperty("status").GetString());
    }

    [Fact]
    public async Task AdminApiTokens_CreateRejectsDuplicateActiveIntegrationName()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var adminJwt = await CreateAdminAndLoginAsync($"td-{suffix}@example.com");
        UseBearer(adminJwt);
        await CreateApiTokenAsync("My API", "fuel:read");

        var duplicateResponse = await _client.PostAsJsonAsync("/api/admin/api-tokens", new
        {
            name = "  my   api  ",
            scopes = "fuel:read",
            expiresAt = (DateTime?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
        using var doc = await JsonDocument.ParseAsync(await duplicateResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Інтеграція з такою назвою вже існує.", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task AdminComments_ListSupportsSearchFiltersAndPagination()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var adminJwt = await CreateAdminAndLoginAsync($"ca-{suffix}@example.com");
        var registeredEmail = $"cr-{suffix}@example.com";
        var deletedEmail = $"cd-{suffix}@example.com";
        await RegisterAsync(registeredEmail);
        await RegisterAsync(deletedEmail);
        var registeredCommentId = await SeedCommentAsync(registeredEmail, $"Needle comment {suffix}", DateTime.UtcNow.AddDays(-2));
        await SeedCommentAsync(deletedEmail, $"Deleted author comment {suffix}", DateTime.UtcNow.AddDays(-1));
        await MarkDeletedAsync(deletedEmail);

        UseBearer(adminJwt);
        var dateFrom = $"{DateTime.UtcNow.AddDays(-3):yyyy-MM-dd}";
        var dateTo = $"{DateTime.UtcNow:yyyy-MM-dd}";
        var filteredResponse = await _client.GetAsync($"/api/admin/comments?search=Needle&author=registered&status=published&dateFrom={dateFrom}&dateTo={dateTo}&page=1&pageSize=10");
        filteredResponse.EnsureSuccessStatusCode();
        using var filteredDoc = await JsonDocument.ParseAsync(await filteredResponse.Content.ReadAsStreamAsync());
        var filteredRoot = filteredDoc.RootElement;
        var filteredComment = Assert.Single(filteredRoot.GetProperty("items").EnumerateArray());

        Assert.Equal(1, filteredRoot.GetProperty("page").GetInt32());
        Assert.Equal(10, filteredRoot.GetProperty("pageSize").GetInt32());
        Assert.Equal(registeredCommentId, filteredComment.GetProperty("id").GetInt32());
        Assert.Equal(registeredEmail, filteredComment.GetProperty("authorEmail").GetString());
        Assert.Equal("registered", filteredComment.GetProperty("authorType").GetString());
        Assert.Equal("published", filteredComment.GetProperty("status").GetString());

        var deletedResponse = await _client.GetAsync($"/api/admin/comments?search=Deleted%20author&author=deleted&page=1&pageSize=10");
        deletedResponse.EnsureSuccessStatusCode();
        using var deletedDoc = await JsonDocument.ParseAsync(await deletedResponse.Content.ReadAsStreamAsync());
        var deletedComment = Assert.Single(deletedDoc.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(deletedEmail, deletedComment.GetProperty("authorEmail").GetString());
        Assert.Equal("deleted", deletedComment.GetProperty("authorType").GetString());

        var guestResponse = await _client.GetAsync("/api/admin/comments?author=guest&page=1&pageSize=10");
        guestResponse.EnsureSuccessStatusCode();
        using var guestDoc = await JsonDocument.ParseAsync(await guestResponse.Content.ReadAsStreamAsync());
        Assert.Equal(0, guestDoc.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ProfileApiTokens_AreNotAvailableForUsers()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var jwt = await RegisterAndLoginAsync($"pt1-{suffix}@example.com");

        UseBearer(jwt);
        var createResponse = await _client.PostAsJsonAsync("/api/profile/api-tokens", new
        {
            name = $"Own API {suffix}",
            scopes = "read:all",
            expiresAt = (DateTime?)null
        });
        Assert.Equal(HttpStatusCode.NotFound, createResponse.StatusCode);

        var listResponse = await _client.GetAsync($"/api/profile/api-tokens?search={Uri.EscapeDataString($"Own API {suffix}")}&status=active&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.NotFound, listResponse.StatusCode);

        var revokeResponse = await _client.DeleteAsync("/api/profile/api-tokens/1");
        Assert.Equal(HttpStatusCode.NotFound, revokeResponse.StatusCode);
    }

    private async Task<string> RegisterAndLoginAsync(string email)
    {
        await RegisterAsync(email);
        return await LoginAsync(email);
    }

    private async Task<string> CreateAdminAndLoginAsync(string email)
    {
        await RegisterAsync(email);
        await SetRoleAsync(email, UserRole.Admin);
        return await LoginAsync(email);
    }

    private async Task<string> CreateApiTokenAsync(string name, string scopes)
    {
        var createResponse = await _client.PostAsJsonAsync("/api/admin/api-tokens", new
        {
            name,
            scopes,
            expiresAt = (DateTime?)null
        });

        createResponse.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await createResponse.Content.ReadAsStreamAsync());
        var apiToken = doc.RootElement.GetProperty("token").GetString();
        Assert.StartsWith("lfm_", apiToken);
        return apiToken!;
    }

    private async Task<int> GetApiTokenIdAsync(string name)
    {
        var listResponse = await _client.GetAsync($"/api/admin/api-tokens?search={Uri.EscapeDataString(name)}&pageSize=100");
        listResponse.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await listResponse.Content.ReadAsStreamAsync());
        return doc.RootElement.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("name").GetString() == name)
            .GetProperty("id")
            .GetInt32();
    }

    private async Task RegisterAsync(string email)
    {
        var password = "password123";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            confirmPassword = password,
            displayName = "Admin Test",
            nickname = email.Split('@')[0].Replace(".", "-")
        });

        response.EnsureSuccessStatusCode();
    }

    private async Task<string> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "password123"
        });

        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return doc.RootElement.GetProperty("token").GetString()!;
    }

    private async Task SetRoleAsync(string email, UserRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
        var user = db.Users.Single(x => x.Email == email);
        user.Role = role;
        await db.SaveChangesAsync();
    }

    private async Task SetProviderAsync(string email, string provider, string externalProviderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
        var user = db.Users.Single(x => x.Email == email);
        user.AuthProvider = provider;
        user.ExternalProviderId = externalProviderId;
        await db.SaveChangesAsync();
    }

    private async Task MarkDeletedAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
        var user = db.Users.Single(x => x.Email == email);
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task<int> SeedCommentAsync(string email, string content, DateTime createdAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
        var user = db.Users.Single(x => x.Email == email);
        var station = db.Stations.OrderBy(x => x.Id).First();
        var comment = new Comment
        {
            UserId = user.Id,
            StationId = station.Id,
            Content = content,
            Rating = 5,
            CreatedAt = createdAt
        };

        db.Comments.Add(comment);
        await db.SaveChangesAsync();
        return comment.Id;
    }

    private void UseBearer(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
