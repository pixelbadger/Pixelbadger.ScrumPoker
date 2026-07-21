namespace Pixelbadger.ScrumPoker.Web.Models;

/// <summary>The card deck: Fibonacci values plus infinity and coffee break.</summary>
public static class Deck
{
    public const string Infinity = "∞";
    public const string Coffee = "☕";

    public static readonly IReadOnlyList<string> Cards =
        ["0", "1", "2", "3", "5", "8", "13", "21", "34", "55", "89", Infinity, Coffee];

    public static bool IsValid(string value) => Cards.Contains(value);
}

public sealed class Participant
{
    /// <summary>Secret key held by the participant's browser; used to reconnect and rejoin.</summary>
    public required string Key { get; init; }
    public required string Name { get; set; }
    public bool IsOwner { get; init; }
    public string? Vote { get; set; }
    public HashSet<string> ConnectionIds { get; } = new();
    public bool IsConnected => ConnectionIds.Count > 0;
}

public sealed class PokerSession
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Secret key returned to the session creator; proves PO identity on join.</summary>
    public required string OwnerKey { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;
    public int RoundNumber { get; set; } = 1;
    public bool Revealed { get; set; }
    public Dictionary<string, Participant> Participants { get; } = new();

    /// <summary>All state mutations and snapshot reads happen under this lock.</summary>
    public object SyncRoot { get; } = new();
}
