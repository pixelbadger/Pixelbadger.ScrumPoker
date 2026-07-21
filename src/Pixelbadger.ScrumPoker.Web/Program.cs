using Pixelbadger.ScrumPoker.Web.Hubs;
using Pixelbadger.ScrumPoker.Web.Persistence;
using Pixelbadger.ScrumPoker.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<PersistenceQueue>();
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddHostedService<SqlitePersistenceService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Session creation stays DB-free: the store enqueues the write for the background service.
app.MapPost("/api/sessions", (CreateSessionRequest request, SessionStore store) =>
{
    var name = string.IsNullOrWhiteSpace(request.Name) ? "Planning Poker" : request.Name.Trim();
    var session = store.CreateSession(name);
    return Results.Ok(new { sessionId = session.Id, ownerKey = session.OwnerKey });
});

app.MapGet("/api/sessions/{sessionId}", (string sessionId, SessionStore store) =>
    store.TryGetSession(sessionId, out var session)
        ? Results.Ok(new { sessionId = session.Id, name = session.Name })
        : Results.NotFound());

app.MapHub<PokerHub>("/hubs/poker");

app.MapFallbackToFile("index.html");

// Idle sessions are pruned opportunistically; history already persisted stays in SQLite.
var pruneTimer = new Timer(
    _ => app.Services.GetRequiredService<SessionStore>().PruneIdleSessions(TimeSpan.FromHours(24)),
    null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
app.Lifetime.ApplicationStopping.Register(pruneTimer.Dispose);

app.Run();

internal sealed record CreateSessionRequest(string? Name);
