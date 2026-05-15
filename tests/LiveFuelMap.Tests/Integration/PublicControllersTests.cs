using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.DAL.Entities;
using LiveFuelMap.DAL.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LiveFuelMap.Tests.Integration;

public sealed class PublicControllersTests(LiveFuelMapApiFactory factory) : IClassFixture<LiveFuelMapApiFactory>
{
    private readonly LiveFuelMapApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetStations_ReturnsSeedStations()
    {
        var response = await _client.GetAsync("/api/stations?page=1&pageSize=5");

        response.EnsureSuccessStatusCode();
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(json.RootElement.GetProperty("items").GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetFuels_ReturnsStableFuelCodes()
    {
        var fuels = await _client.GetFromJsonAsync<JsonElement[]>("/api/fuels");

        Assert.NotNull(fuels);
        Assert.Contains(fuels!, fuel => fuel.GetProperty("code").GetString() == "diesel");
        Assert.Contains(fuels!, fuel => fuel.GetProperty("code").GetString() == "a95plus");
    }

    [Fact]
    public async Task GetStationById_ReturnsStationDetails()
    {
        var response = await _client.GetAsync("/api/stations/1");

        response.EnsureSuccessStatusCode();
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("AMIC", json.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task CreateSubscription_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/subscriptions", new
        {
            fuelId = 2,
            city = "Харків",
            frequency = "Daily"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExternalCurrentPrices_WithoutApiToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/external/current-prices");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetComments_ReturnsPublicCommentList()
    {
        var response = await _client.GetAsync("/api/comments?stationId=1");

        response.EnsureSuccessStatusCode();
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
    }

    [Fact]
    public async Task ExportFuelPriceReport_AsExcel_ReturnsWorkbook()
    {
        var request = new FuelPriceExportRequest(
            "Харків",
            [1],
            ["a95"],
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 30),
            "excel");

        var response = await _client.PostAsJsonAsync("/api/reports/fuel-prices/export", request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("fuel-prices-report-20260401-20260430.xlsx", response.Content.Headers.ContentDisposition?.FileName);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public async Task ExportFuelPriceReport_AsPdf_ReturnsPdf()
    {
        var request = new FuelPriceExportRequest(
            "Харків",
            [1],
            ["a95"],
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 30),
            "pdf");

        var response = await _client.PostAsJsonAsync("/api/reports/fuel-prices/export", request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("fuel-prices-report-20260401-20260430.pdf", response.Content.Headers.ContentDisposition?.FileName);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }

    [Fact]
    public async Task CreateComment_ForNewAccount_IsDelayed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
        var commentService = scope.ServiceProvider.GetRequiredService<ICommentService>();
        var user = new User
        {
            Email = $"new-comment-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            commentService.CreateAsync(user.Id, new CreateCommentRequest(1, null, "test", 5)));
        Assert.Contains("Comments are available", ex.Message);
    }

    [Fact]
    public async Task CreateComment_ForMatureAccount_SavesComment()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
        var commentService = scope.ServiceProvider.GetRequiredService<ICommentService>();
        var user = new User
        {
            Email = $"old-comment-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow.AddMinutes(-11)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var comment = await commentService.CreateAsync(user.Id, new CreateCommentRequest(1, null, "price looks fresh", 4));

        Assert.Equal(1, comment.StationId);
        Assert.Equal("price looks fresh", comment.Content);
        Assert.Equal(4, comment.Rating);
    }

    [Fact]
    public async Task CreateComment_WithoutRating_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveFuelMapDbContext>();
        var commentService = scope.ServiceProvider.GetRequiredService<ICommentService>();
        var user = new User
        {
            Email = $"no-rating-comment-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow.AddMinutes(-11)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            commentService.CreateAsync(user.Id, new CreateCommentRequest(1, null, "rating required")));

        Assert.Contains("Rating is required", ex.Message);
    }
}
