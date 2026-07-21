# CLAUDE.md

Real-time planning poker: ASP.NET Core 8 + SignalR backend, React/TypeScript (Vite) client, SQLite history.

## Build & run

```bash
# Client (builds into src/Pixelbadger.ScrumPoker.Web/wwwroot — gitignored)
cd client && npm install && npm run build   # tsc -b && vite build

# Server (serves API, hub, and the built SPA)
cd src/Pixelbadger.ScrumPoker.Web && dotnet run
```

Dev loop: run the backend with `ASPNETCORE_URLS=http://localhost:5080`, then `npm run dev` in
`client/` (Vite proxies `/api` and `/hubs` to 5080, websockets included). There are no automated
tests; verify hub behaviour with a scripted `@microsoft/signalr` client against a running server.

## Architecture invariants

- **Hot paths never touch the database.** `PokerHub` and the minimal-API endpoints in
  `Program.cs` work only against the in-memory `SessionStore`. Durable writes go through
  `PersistenceQueue` (unbounded channel) and are performed solely by `SqlitePersistenceService`
  (a `BackgroundService`, the only SQLite writer). Keep it that way when adding features.
- **Retention is best-effort by design.** A failed DB write is logged and dropped, never retried
  on the hot path. Live-session consistency comes from memory, not the DB.
- **Concurrency model**: `SessionStore` uses a `ConcurrentDictionary` of sessions; every mutation
  and snapshot read locks the session's `SyncRoot`. Snapshots (`SessionView`) are immutable
  records — build them inside the lock, broadcast outside.
- **Vote visibility** is enforced server-side: `SessionView` carries `vote: null` until the round
  is revealed (clients only ever see `hasVoted`). Don't leak votes in new DTOs.
- **Identity is key-based, not connection-based**: the creator's `ownerKey` proves PO status on
  join; each participant's `participantKey` (kept in browser localStorage, see
  `client/src/lib/storage.ts`) lets refreshes/reconnects reattach to the same seat. Keys are
  secrets — never include them in broadcast state.

## Domain rules

- Deck: Fibonacci `0–89` plus `∞` and `☕` (`Deck` in `Models/PokerSession.cs`; the server
  validates every vote against it and the client renders whatever deck the server sends).
- Votes may be changed at any time, including after reveal.
- Only the PO can reveal; reset requires an active reveal and enqueues the finished round
  (`RoundCompletedEvent`) with whatever votes stood at reset time.
- Idle sessions are pruned from memory after 24h (timer in `Program.cs`); SQLite history remains.

## Layout

- `src/Pixelbadger.ScrumPoker.Web/` — `Program.cs` (endpoints, DI, prune timer),
  `Hubs/PokerHub.cs`, `Services/SessionStore.cs`, `Persistence/` (queue + SQLite writer),
  `Models/` (domain + view records).
- `client/src/` — `pages/` (Home, SessionPage), `components/PokerRoom.tsx`,
  `lib/usePokerSession.ts` (SignalR hook), `lib/storage.ts`, `styles.css`.
- SQLite path: `Database:Path` config (default `scrumpoker.db`); schema is created on startup.
