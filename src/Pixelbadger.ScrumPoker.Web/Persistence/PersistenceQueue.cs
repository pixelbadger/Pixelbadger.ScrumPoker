using System.Threading.Channels;

namespace Pixelbadger.ScrumPoker.Web.Persistence;

public abstract record PersistenceEvent;

public sealed record SessionCreatedEvent(string SessionId, string Name, DateTimeOffset CreatedAt) : PersistenceEvent;

public sealed record VoteRecord(string ParticipantName, string Vote);

public sealed record RoundCompletedEvent(
    string SessionId,
    int RoundNumber,
    DateTimeOffset CompletedAt,
    IReadOnlyList<VoteRecord> Votes) : PersistenceEvent;

/// <summary>
/// Write-behind buffer between the in-memory session store and SQLite. Hub and API code
/// only ever enqueue here; the background service owns all database writes.
/// </summary>
public sealed class PersistenceQueue
{
    private readonly Channel<PersistenceEvent> _channel =
        Channel.CreateUnbounded<PersistenceEvent>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(PersistenceEvent evt) => _channel.Writer.TryWrite(evt);

    public ChannelReader<PersistenceEvent> Reader => _channel.Reader;
}
