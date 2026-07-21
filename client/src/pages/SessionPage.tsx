import { FormEvent, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { participantKeyFor, saveName, savedName } from '../lib/storage';
import { PokerRoom } from '../components/PokerRoom';

export function SessionPage() {
  const { sessionId = '' } = useParams();
  const [name, setName] = useState<string | null>(() =>
    // A returning participant (has a key for this session) skips the join form.
    participantKeyFor(sessionId) && savedName() ? savedName() : null,
  );
  const [exists, setExists] = useState<boolean | null>(null);

  useEffect(() => {
    fetch(`/api/sessions/${sessionId}`)
      .then((r) => setExists(r.ok))
      .catch(() => setExists(false));
  }, [sessionId]);

  if (exists === null) {
    return <main className="page page-center">Loading…</main>;
  }

  if (!exists) {
    return (
      <main className="page page-center">
        <div className="panel">
          <h1>Session not found</h1>
          <p className="muted">
            This session may have ended or the link may be wrong. Ask the product
            owner for a fresh invite, or <a href="/">create a new session</a>.
          </p>
        </div>
      </main>
    );
  }

  if (!name) {
    return <JoinForm onJoin={setName} />;
  }

  return <PokerRoom sessionId={sessionId} name={name} />;
}

function JoinForm({ onJoin }: { onJoin: (name: string) => void }) {
  const [name, setName] = useState(savedName());

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const trimmed = name.trim();
    if (!trimmed) return;
    saveName(trimmed);
    onJoin(trimmed);
  };

  return (
    <main className="page page-center">
      <div className="panel">
        <h1>Join session</h1>
        <p className="muted">Enter your name to join the estimation session.</p>
        <form onSubmit={submit}>
          <label>
            Your name
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              maxLength={40}
              autoFocus
              required
            />
          </label>
          <button type="submit" disabled={!name.trim()}>
            Join
          </button>
        </form>
      </div>
    </main>
  );
}
