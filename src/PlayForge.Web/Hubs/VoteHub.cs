using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PlayForge.Web.Hubs;

/// <summary>
/// Minimal hub: manages SignalR group membership so future JS/mobile clients
/// can receive the same TallyUpdated / SessionStarted / SessionClosed messages
/// that the Blazor components receive via VoteNotifier.
/// </summary>
[Authorize]
public class VoteHub : Hub
{
    public Task JoinGroup(string groupId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"group-{groupId}");

    public Task LeaveGroup(string groupId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group-{groupId}");
}
