using Microsoft.AspNetCore.SignalR;
using Pixelbadger.ScrumPoker.Web.Models;
using Pixelbadger.ScrumPoker.Web.Services;

namespace Pixelbadger.ScrumPoker.Web.Hubs;

/// <summary>
/// Real-time session traffic. Every method works purely against the in-memory
/// <see cref="SessionStore"/>; nothing here touches the database.
/// </summary>
public sealed class PokerHub(SessionStore store) : Hub
{
    private const string StateEvent = "state";

    public async Task<JoinResult?> Join(string sessionId, string name, string? participantKey, string? ownerKey)
    {
        var result = store.Join(sessionId, Context.ConnectionId, name, participantKey, ownerKey);
        if (result is null)
        {
            return null;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        await Clients.OthersInGroup(sessionId).SendAsync(StateEvent, result.State);
        return result;
    }

    public async Task CastVote(string value)
    {
        await BroadcastAsync(store.CastVote(Context.ConnectionId, value));
    }

    public async Task Reveal()
    {
        await BroadcastAsync(store.Reveal(Context.ConnectionId));
    }

    public async Task Reset()
    {
        await BroadcastAsync(store.Reset(Context.ConnectionId));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var result = store.Disconnect(Context.ConnectionId);
        if (result is not null)
        {
            await Clients.Group(result.Value.SessionId).SendAsync(StateEvent, result.Value.State);
        }
        await base.OnDisconnectedAsync(exception);
    }

    private Task BroadcastAsync(SessionView? state) =>
        state is null ? Task.CompletedTask : Clients.Group(state.Id).SendAsync(StateEvent, state);
}
