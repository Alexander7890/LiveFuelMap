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
        Assert.DoesNotContain(doc.RootElement.EnumerateArray(), item => item.GetProperty("id").GetInt32() == tokenId);
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
        var listResponse = await _client.GetAsync("/api/admin/api-tokens");
        listResponse.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await listResponse.Content.ReadAsStreamAsync());
        return doc.RootElement.EnumerateArray()
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

    private void UseBearer(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
