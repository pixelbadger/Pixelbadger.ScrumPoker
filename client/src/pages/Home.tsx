import { FormEvent, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { saveName, saveOwnerKey, savedName } from '../lib/storage';

export function Home() {
  const navigate = useNavigate();
  const [sessionName, setSessionName] = useState('');
  const [ownerName, setOwnerName] = useState(savedName());
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const createSession = async (event: FormEvent) => {
    event.preventDefault();
    if (!ownerName.trim()) return;
    setBusy(true);
    setError(null);
    try {
      const response = await fetch('/api/sessions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: sessionName }),
      });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const { sessionId, ownerKey } = await response.json();
      saveOwnerKey(sessionId, ownerKey);
      saveName(ownerName.trim());
      navigate(`/session/${sessionId}`);
    } catch {
      setError('Could not create the session. Is the server running?');
      setBusy(false);
    }
  };

  return (
    <main className="page page-center">
      <div className="panel">
        <h1>🃏 Scrum Poker</h1>
        <p className="muted">
          Create a session, share the link with your team, and estimate together.
        </p>
        <form onSubmit={createSession}>
          <label>
            Session name
            <input
              value={sessionName}
              onChange={(e) => setSessionName(e.target.value)}
              placeholder="Sprint 42 planning"
              maxLength={80}
            />
          </label>
          <label>
            Your name
            <input
              value={ownerName}
              onChange={(e) => setOwnerName(e.target.value)}
              placeholder="Product owner"
              maxLength={40}
              required
            />
          </label>
          <button type="submit" disabled={busy || !ownerName.trim()}>
            {busy ? 'Creating…' : 'Create session'}
          </button>
          {error && <p className="error">{error}</p>}
        </form>
      </div>
    </main>
  );
}
