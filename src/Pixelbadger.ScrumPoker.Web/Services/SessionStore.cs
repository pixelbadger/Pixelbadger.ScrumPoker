using System.Collections.Concurrent;
using System.Security.Cryptography;
using Pixelbadger.ScrumPoker.Web.Models;
using Pixelbadger.ScrumPoker.Web.Persistence;

namespace Pixelbadger.ScrumPoker.Web.Services;

/// <summary>
/// Authoritative in-memory store for live sessions. All reads and writes on the hot path
/// happen here; durable history is handed off to the <see cref="PersistenceQueue"/>.
/// </summary>
public sealed class SessionStore(PersistenceQueue persistenceQueue)
{
    // Characters chosen to avoid ambiguous glyphs in shared links (no 0/O, 1/l/i).
    private const string IdAlphabet = "abcdefghjkmnpqrstuvwxyz23456789";

    private readonly ConcurrentDictionary<string, PokerSession> _sessions = new();
    private readonly ConcurrentDictionary<string, (string SessionId, string ParticipantKey)> _connections = new();

    public PokerSession CreateSession(string name)
    {
        while (true)
        {
            var session = new PokerSession
            {
                Id = GenerateId(6),
                Name = name,
                OwnerKey = Guid.NewGuid().ToString("N"),
            };
            if (_sessions.TryAdd(session.Id, session))
            {
                persistenceQueue.Enqueue(new SessionCreatedEvent(session.Id, session.Name, session.CreatedAt));
                return session;
            }
        }
    }

    public bool TryGetSession(string sessionId, out PokerSession session) =>
        _sessions.TryGetValue(sessionId, out session!);

    /// <summary>
    /// Adds a participant, or reattaches an existing one when a known participant key is
    /// supplied (page refresh / reconnect). Presenting the session's owner key makes the
    /// participant the PO.
    /// </summary>
    public JoinResult? Join(string sessionId, string connectionId, string name, string? participantKey, string? ownerKey)
    {
        if (!TryGetSession(sessionId, out var session))
        {
            return null;
        }

        lock (session.SyncRoot)
        {
            Participant? participant = null;
            if (participantKey is not null)
            {
                session.Participants.TryGetValue(participantKey, out participant);
            }

            if (participant is null)
            {
                participant = new Participant
                {
                    Key = Guid.NewGuid().ToString("N"),
                    Name = name,
                    IsOwner = ownerKey == session.OwnerKey,
                };
                session.Participants[participant.Key] = participant;
            }
            else if (!string.IsNullOrWhiteSpace(name))
            {
                participant.Name = name;
            }

            participant.ConnectionIds.Add(connectionId);
            _connections[connectionId] = (sessionId, participant.Key);
            session.LastActivityAt = DateTimeOffset.UtcNow;

            return new JoinResult(participant.Key, participant.IsOwner, participant.Vote, SnapshotLocked(session));
        }
    }

    /// <summary>Casts or changes a vote. Changes are allowed at any time, including after reveal.</summary>
    public SessionView? CastVote(string connectionId, string value)
    {
        if (!Deck.IsValid(value) || !TryGetParticipant(connectionId, out var session, out var participant))
        {
            return null;
        }

        lock (session.SyncRoot)
        {
            participant.Vote = value;
            session.LastActivityAt = DateTimeOffset.UtcNow;
            return SnapshotLocked(session);
        }
    }

    public SessionView? Reveal(string connectionId)
    {
        if (!TryGetParticipant(connectionId, out var session, out var participant) || !participant.IsOwner)
        {
            return null;
        }

        lock (session.SyncRoot)
        {
            session.Revealed = true;
            session.LastActivityAt = DateTimeOffset.UtcNow;
            return SnapshotLocked(session);
        }
    }

    /// <summary>
    /// PO starts the next round. Only valid after a reveal; the finished round is queued
    /// for persistence with whatever votes stood at reset time.
    /// </summary>
    public SessionView? Reset(string connectionId)
    {
        if (!TryGetParticipant(connectionId, out var session, out var participant) || !participant.IsOwner)
        {
            return null;
        }

        lock (session.SyncRoot)
        {
            if (!session.Revealed)
            {
                return null;
            }

            var votes = session.Participants.Values
                .Where(p => p.Vote is not null)
                .Select(p => new VoteRecord(p.Name, p.Vote!))
                .ToList();
            persistenceQueue.Enqueue(
                new RoundCompletedEvent(session.Id, session.RoundNumber, DateTimeOffset.UtcNow, votes));

            session.RoundNumber++;
            session.Revealed = false;
            foreach (var p in session.Participants.Values)
            {
                p.Vote = null;
            }
            session.LastActivityAt = DateTimeOffset.UtcNow;
            return SnapshotLocked(session);
        }
    }

    /// <summary>Returns the session snapshot to broadcast, or null if the connection was unknown.</summary>
    public (string SessionId, SessionView State)? Disconnect(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var link) ||
            !TryGetSession(link.SessionId, out var session))
        {
            return null;
        }

        lock (session.SyncRoot)
        {
            if (session.Participants.TryGetValue(link.ParticipantKey, out var participant))
            {
                participant.ConnectionIds.Remove(connectionId);
            }
            return (session.Id, SnapshotLocked(session));
        }
    }

    public SessionView? Snapshot(string sessionId)
    {
        if (!TryGetSession(sessionId, out var session))
        {
            return null;
        }
        lock (session.SyncRoot)
        {
            return SnapshotLocked(session);
        }
    }

    /// <summary>Drops sessions idle for longer than <paramref name="maxIdle"/>. Low-stakes by design.</summary>
    public void PruneIdleSessions(TimeSpan maxIdle)
    {
        var cutoff = DateTimeOffset.UtcNow - maxIdle;
        foreach (var (id, session) in _sessions)
        {
            if (session.LastActivityAt < cutoff)
            {
                _sessions.TryRemove(id, out _);
            }
        }
    }

    private bool TryGetParticipant(string connectionId, out PokerSession session, out Participant participant)
    {
        session = null!;
        participant = null!;
        return _connections.TryGetValue(connectionId, out var link) &&
               TryGetSession(link.SessionId, out session) &&
               session.Participants.TryGetValue(link.ParticipantKey, out participant!);
    }

    private static SessionView SnapshotLocked(PokerSession session)
    {
        var participants = session.Participants.Values
            .OrderBy(p => !p.IsOwner)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => new ParticipantView(
                p.Name,
                p.IsOwner,
                HasVoted: p.Vote is not null,
                Connected: p.IsConnected,
                Vote: session.Revealed ? p.Vote : null))
            .ToList();

        var connected = session.Participants.Values.Where(p => p.IsConnected).ToList();
        var allVoted = connected.Count > 0 && connected.All(p => p.Vote is not null);

        return new SessionView(session.Id, session.Name, session.RoundNumber, session.Revealed,
            allVoted, participants, Deck.Cards);
    }

    private static string GenerateId(int length)
    {
        Span<char> chars = stackalloc char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = IdAlphabet[RandomNumberGenerator.GetInt32(IdAlphabet.Length)];
        }
        return new string(chars);
    }
}
