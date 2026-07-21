import { useState } from 'react';
import { usePokerSession } from '../lib/usePokerSession';
import { SessionView } from '../lib/types';

export function PokerRoom({ sessionId, name }: { sessionId: string; name: string }) {
  const { status, state, isOwner, myVote, castVote, reveal, reset } =
    usePokerSession(sessionId, name);

  if (status === 'error') {
    return (
      <main className="page page-center">
        <div className="panel">
          <h1>Connection problem</h1>
          <p className="muted">Could not join the session. Try reloading the page.</p>
        </div>
      </main>
    );
  }

  if (!state) {
    return <main className="page page-center">Connecting…</main>;
  }

  return (
    <main className="page">
      <header className="room-header">
        <div>
          <h1>{state.name}</h1>
          <p className="muted">
            Round {state.round}
            {status === 'reconnecting' && ' · reconnecting…'}
          </p>
        </div>
        <InviteLink />
      </header>

      <ParticipantList state={state} />

      {state.revealed && <Results state={state} />}

      {isOwner && (
        <div className="owner-controls">
          <button onClick={reveal} disabled={state.revealed} title={state.allVoted ? '' : 'Not everyone has voted yet'}>
            {state.allVoted || state.revealed ? 'Reveal votes' : 'Reveal votes (early)'}
          </button>
          <button onClick={reset} disabled={!state.revealed}>
            New round
          </button>
        </div>
      )}

      <section className="deck-section">
        <h2>{state.revealed ? 'Votes are revealed — you can still change yours' : 'Pick your estimate'}</h2>
        <div className="deck">
          {state.deck.map((card) => (
            <button
              key={card}
              className={`card ${myVote === card ? 'selected' : ''}`}
              onClick={() => castVote(card)}
            >
              {card}
            </button>
          ))}
        </div>
      </section>
    </main>
  );
}

function InviteLink() {
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(window.location.href);
    } catch {
      // Clipboard may be unavailable (http, permissions); fall back to prompt.
      window.prompt('Copy this invite link:', window.location.href);
    }
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <button className="secondary" onClick={copy}>
      {copied ? 'Link copied!' : '🔗 Copy invite link'}
    </button>
  );
}

function ParticipantList({ state }: { state: SessionView }) {
  return (
    <section>
      <h2>Team ({state.participants.length})</h2>
      <ul className="participants">
        {state.participants.map((p, i) => (
          <li key={i} className={p.connected ? '' : 'offline'}>
            <span className="participant-name">
              {p.name}
              {p.isOwner && <span className="badge">PO</span>}
              {!p.connected && <span className="badge offline-badge">away</span>}
            </span>
            <span className={`vote-slot ${p.hasVoted ? 'voted' : ''}`}>
              {state.revealed ? (p.vote ?? '–') : p.hasVoted ? '✓' : '…'}
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}

function Results({ state }: { state: SessionView }) {
  const votes = state.participants
    .filter((p) => p.vote !== null)
    .map((p) => p.vote!);
  const numeric = votes.map(Number).filter((n) => !Number.isNaN(n));
  const average =
    numeric.length > 0
      ? (numeric.reduce((a, b) => a + b, 0) / numeric.length).toFixed(1)
      : null;
  const consensus = votes.length > 1 && votes.every((v) => v === votes[0]);

  return (
    <section className="results">
      <h2>Results</h2>
      {average !== null && (
        <p>
          Average: <strong>{average}</strong>
          {consensus && ' · 🎉 consensus!'}
        </p>
      )}
      {average === null && votes.length > 0 && <p className="muted">No numeric votes this round.</p>}
    </section>
  );
}
