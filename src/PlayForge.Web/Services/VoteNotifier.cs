using PlayForge.Application.DTOs;

namespace PlayForge.Web.Services;

/// <summary>
/// Singleton event bus that lets Blazor Server circuits subscribe to vote state changes
/// without polling the database. Each GroupDetail page subscribes on mount and
/// calls InvokeAsync(StateHasChanged) when an event fires for its group.
/// </summary>
public class VoteNotifier
{
    public event Action<Guid, VoteSessionDto>? SessionStarted;
    public event Action<Guid, VoteTallyDto>?   TallyUpdated;
    public event Action<Guid, VoteSessionDto>? SessionClosed;

    public void NotifySessionStarted(Guid groupId, VoteSessionDto session)
        => SessionStarted?.Invoke(groupId, session);

    public void NotifyTallyUpdated(Guid groupId, VoteTallyDto tally)
        => TallyUpdated?.Invoke(groupId, tally);

    public void NotifySessionClosed(Guid groupId, VoteSessionDto session)
        => SessionClosed?.Invoke(groupId, session);
}
