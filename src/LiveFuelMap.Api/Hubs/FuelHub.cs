using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LiveFuelMap.Api.Hubs;

public sealed class FuelHub(
    IFuelDataService fuelDataService,
    ICommentService commentService,
    LivePresenceTracker presenceTracker) : Hub
{
    public override async Task OnConnectedAsync()
    {
        presenceTracker.TrackConnected(Context.ConnectionId, GetUserId());
        await Clients.Caller.SendAsync("onlineStatus", presenceTracker.Snapshot(), Context.ConnectionAborted);
        await Clients.All.SendAsync("onlineStatusChanged", presenceTracker.Snapshot(), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        presenceTracker.TrackDisconnected(Context.ConnectionId);
        await Clients.All.SendAsync("onlineStatusChanged", presenceTracker.Snapshot(), CancellationToken.None);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task<object> GetFuelData()
    {
        var result = await fuelDataService.GetStationsAsync(new StationListQuery(null, null, null), Context.ConnectionAborted);
        var fuels = await fuelDataService.GetFuelsAsync(Context.ConnectionAborted);
        return new { fuels, stations = result.Items };
    }

    public Task<PriceHistoryDto> GetPriceHistory(PriceHistoryQuery query) =>
        fuelDataService.GetPriceHistoryAsync(query, Context.ConnectionAborted);

    public Task<IReadOnlyList<CommentDto>> GetComments(int? stationId) =>
        stationId is null
            ? commentService.ListAsync(take: 200, cancellationToken: Context.ConnectionAborted)
            : commentService.GetByStationAsync(stationId.Value, Context.ConnectionAborted);

    public Task<RealtimePresenceDto> GetOnlineStatus() =>
        Task.FromResult(presenceTracker.Snapshot());

    [Authorize]
    public async Task<CommentDto> SendComment(CreateCommentRequest request)
    {
        var userId = GetUserId() ?? throw new HubException("Authentication is required.");
        try
        {
            var comment = await commentService.CreateAsync(userId, request, Context.ConnectionAborted);
            await Clients.All.SendAsync("commentCreated", comment, Context.ConnectionAborted);
            return comment;
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    [Authorize]
    public async Task<CommentDto> UpdateComment(int id, UpdateCommentRequest request)
    {
        var userId = GetUserId() ?? throw new HubException("Authentication is required.");
        try
        {
            var comment = await commentService.UpdateOwnAsync(userId, id, request, Context.ConnectionAborted)
                ?? throw new HubException("Comment not found.");
            await Clients.All.SendAsync("commentUpdated", comment, Context.ConnectionAborted);
            return comment;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new HubException(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    [Authorize]
    public async Task DeleteComment(int id)
    {
        var userId = GetUserId() ?? throw new HubException("Authentication is required.");
        try
        {
            var comment = await commentService.DeleteOwnAsync(userId, id, Context.ConnectionAborted)
                ?? throw new HubException("Comment not found.");
            await Clients.All.SendAsync("commentDeleted", new { comment.Id, comment.StationId }, Context.ConnectionAborted);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    private int? GetUserId()
    {
        var value = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, out var userId) ? userId : null;
    }
}

public sealed class LivePresenceTracker
{
    private readonly ConcurrentDictionary<string, int?> _connections = new(StringComparer.Ordinal);

    public void TrackConnected(string connectionId, int? userId) =>
        _connections[connectionId] = userId;

    public void TrackDisconnected(string connectionId) =>
        _connections.TryRemove(connectionId, out _);

    public RealtimePresenceDto Snapshot()
    {
        var totalConnections = _connections.Count;
        var authenticatedUsers = _connections.Values
            .Where(userId => userId is not null)
            .Distinct()
            .Count();
        var anonymousConnections = _connections.Values.Count(userId => userId is null);

        return new RealtimePresenceDto(totalConnections, anonymousConnections, authenticatedUsers);
    }
}

public sealed class SignalRFuelUpdatesNotifier(
    IHubContext<FuelHub> hubContext,
    IFuelDataService fuelDataService,
    ISubscriptionNotificationQueue subscriptionNotificationQueue) : IFuelUpdatesNotifier
{
    public Task NotifyFuelDataUpdatedAsync(CancellationToken cancellationToken = default) =>
        NotifyFuelDataUpdatedAsync([], cancellationToken);

    public async Task NotifyFuelDataUpdatedAsync(
        IReadOnlyList<PriceChangeNotificationDto> priceChanges,
        CancellationToken cancellationToken = default)
    {
        var stations = await fuelDataService.GetStationsAsync(new StationListQuery(null, null, null), cancellationToken);
        var fuels = await fuelDataService.GetFuelsAsync(cancellationToken);

        await hubContext.Clients.All.SendAsync("fuelDataUpdate", new { fuels, stations = stations.Items }, cancellationToken);

        foreach (var change in priceChanges)
            await hubContext.Clients.All.SendAsync("priceChanged", change, cancellationToken);

        subscriptionNotificationQueue.QueuePriceChanges(priceChanges);
    }
}
