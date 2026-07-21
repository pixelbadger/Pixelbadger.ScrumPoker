using Microsoft.Data.Sqlite;

namespace Pixelbadger.ScrumPoker.Web.Persistence;

/// <summary>
/// Sole writer to the SQLite database. Drains the <see cref="PersistenceQueue"/> so that
/// SignalR and API hot paths never block on disk I/O. Retention is best-effort: a failed
/// write is logged and dropped, never retried at the cost of live session traffic.
/// </summary>
public sealed class SqlitePersistenceService(
    PersistenceQueue queue,
    IConfiguration configuration,
    ILogger<SqlitePersistenceService> logger) : BackgroundService
{
    private string ConnectionString =>
        $"Data Source={configuration["Database:Path"] ?? "scrumpoker.db"}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        EnsureSchema();

        await foreach (var evt in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                Persist(evt);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to persist {EventType}; dropping event", evt.GetType().Name);
            }
        }
    }

    private void EnsureSchema()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS sessions (
                id         TEXT PRIMARY KEY,
                name       TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS rounds (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id   TEXT NOT NULL REFERENCES sessions(id),
                round_number INTEGER NOT NULL,
                completed_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS votes (
                round_id         INTEGER NOT NULL REFERENCES rounds(id),
                participant_name TEXT NOT NULL,
                vote             TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private void Persist(PersistenceEvent evt)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        switch (evt)
        {
            case SessionCreatedEvent created:
            {
                using var command = connection.CreateCommand();
                command.CommandText =
                    "INSERT OR IGNORE INTO sessions (id, name, created_at) VALUES ($id, $name, $createdAt)";
                command.Parameters.AddWithValue("$id", created.SessionId);
                command.Parameters.AddWithValue("$name", created.Name);
                command.Parameters.AddWithValue("$createdAt", created.CreatedAt.ToString("O"));
                command.ExecuteNonQuery();
                break;
            }
            case RoundCompletedEvent round:
            {
                using var transaction = connection.BeginTransaction();

                using var roundCommand = connection.CreateCommand();
                roundCommand.Transaction = transaction;
                roundCommand.CommandText = """
                    INSERT INTO rounds (session_id, round_number, completed_at)
                    VALUES ($sessionId, $roundNumber, $completedAt)
                    RETURNING id
                    """;
                roundCommand.Parameters.AddWithValue("$sessionId", round.SessionId);
                roundCommand.Parameters.AddWithValue("$roundNumber", round.RoundNumber);
                roundCommand.Parameters.AddWithValue("$completedAt", round.CompletedAt.ToString("O"));
                var roundId = (long)roundCommand.ExecuteScalar()!;

                foreach (var vote in round.Votes)
                {
                    using var voteCommand = connection.CreateCommand();
                    voteCommand.Transaction = transaction;
                    voteCommand.CommandText =
                        "INSERT INTO votes (round_id, participant_name, vote) VALUES ($roundId, $name, $vote)";
                    voteCommand.Parameters.AddWithValue("$roundId", roundId);
                    voteCommand.Parameters.AddWithValue("$name", vote.ParticipantName);
                    voteCommand.Parameters.AddWithValue("$vote", vote.Vote);
                    voteCommand.ExecuteNonQuery();
                }

                transaction.Commit();
                break;
            }
        }
    }
}
