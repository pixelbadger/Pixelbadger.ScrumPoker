# Pixelbadger.ScrumPoker

Real-time planning poker for sprint estimation. ASP.NET Core + SignalR backend, React (Vite) frontend, SQLite session history.

## How it works

- The product owner creates a session and shares the generated link (`/session/{id}`).
- Teammates open the link, enter their name, and join the lobby.
- Everyone estimates with a Fibonacci deck — `0 1 2 3 5 8 13 21 34 55 89` — plus `∞` and `☕` (coffee break).
- Votes are hidden until the PO reveals them; votes can be changed at any time, before or after reveal.
- After reveal, the PO starts a new round; the finished round is written to SQLite.

## Architecture

- **Live state is in memory** (`SessionStore`): sessions, participants, and votes live in a
  `ConcurrentDictionary` with per-session locking. SignalR hub methods and API endpoints never
  touch the database.
- **Write-behind persistence**: session creation and completed rounds are enqueued on an
  unbounded channel (`PersistenceQueue`); a background service (`SqlitePersistenceService`) is
  the sole SQLite writer. Retention is deliberately best-effort — a failed write is logged and
  dropped, keeping the hot path fast and the live session consistent.
- **Identity**: the creator receives an `ownerKey` (stored in the browser's localStorage) that
  marks them as PO on join; every participant receives a `participantKey` so page refreshes and
  reconnects reattach to the same seat instead of duplicating it.
- Idle sessions are pruned from memory after 24 hours; persisted history stays in SQLite.

## Running

Requires .NET 8 SDK and Node 18+.

```bash
# Build the client into the server's wwwroot
cd client
npm install
npm run build

# Run the server (serves API, SignalR hub, and the SPA)
cd ../src/Pixelbadger.ScrumPoker.Web
dotnet run
```

The SQLite file location is configurable via `Database:Path` in `appsettings.json`
(default `scrumpoker.db` in the working directory).

### Development

Run the backend (`dotnet run`, listens per launch profile) and the Vite dev server side by side:

```bash
cd client
npm run dev   # proxies /api and /hubs to http://localhost:5080
```

Set `ASPNETCORE_URLS=http://localhost:5080` when starting the backend so the proxy target matches.
