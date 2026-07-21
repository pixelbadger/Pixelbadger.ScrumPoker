namespace Pixelbadger.ScrumPoker.Web.Models;

/// <summary>What every client sees. Vote values are only populated once the round is revealed.</summary>
public sealed record ParticipantView(string Name, bool IsOwner, bool HasVoted, bool Connected, string? Vote);

public sealed record SessionView(
    string Id,
    string Name,
    int Round,
    bool Revealed,
    bool AllVoted,
    IReadOnlyList<ParticipantView> Participants,
    IReadOnlyList<string> Deck);

public sealed record JoinResult(string ParticipantKey, bool IsOwner, string? Vote, SessionView State);
