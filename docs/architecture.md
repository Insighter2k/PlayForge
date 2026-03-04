# PlayForge — Architecture

## Overview

PlayForge follows Clean Architecture with a DDD-lite domain model. The codebase is split into four projects that enforce a strict inward-pointing dependency rule: inner layers know nothing about outer layers. The result is a domain and application layer that can be tested without a database, a running web server, or a Steam API key, while the infrastructure and web layers handle all external concerns.

The runtime topology is a single-process Blazor Server application. SignalR circuits live in-process, which eliminates the need for a message bus or a distributed backplane on a single-node deployment.

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        PlayForge.Web                            │
│  Blazor Server pages · SignalR hub · Steam auth handler ·       │
│  Health endpoint · ASP.NET Core pipeline · Razor components     │
└────────┬──────────────────────────┬────────────────────────────┘
         │ depends on               │ depends on
         ▼                          ▼
┌─────────────────────┐   ┌──────────────────────────────────────┐
│ PlayForge.          │   │ PlayForge.Infrastructure              │
│ Application         │   │                                       │
│                     │   │  EF Core · AppDbContext ·            │
│  Services · DTOs ·  │◄──│  Repositories · SteamApiClient ·    │
│  PlatformConnector  │   │  IMemoryCache adapters               │
│  Registry           │   └──────────────────────────────────────┘
└────────┬────────────┘             │ depends on
         │ depends on               │
         ▼                          ▼
┌──────────────────────────────────────────────────────────────────┐
│                      PlayForge.Domain                            │
│  Entities · Value objects · Interfaces · Enums                   │
│  No external NuGet dependencies                                  │
└──────────────────────────────────────────────────────────────────┘
```

**Dependency rule:** arrows point inward only. `PlayForge.Domain` depends on nothing. `PlayForge.Application` depends only on `PlayForge.Domain`. `PlayForge.Infrastructure` depends on `PlayForge.Domain` and `PlayForge.Application` (to implement interfaces defined there). `PlayForge.Web` depends on all three.

---

## Layer Responsibilities

### PlayForge.Domain

The domain layer is the heart of the system. It contains:

- **Entities** — objects with identity (`User`, `Game`, `UserGame`, `GameGroup`, `GroupMembership`, `GroupCandidate`, `VoteSession`, `Vote`). Entities enforce their own invariants through private setters and factory methods. No public parameterless constructors.
- **Value objects** — immutable, equality-by-value types (`SteamId`, `AppId`). They validate on construction and throw a domain exception for invalid input rather than returning null.
- **Interfaces** — contracts implemented by outer layers. The critical one is `IPlatformConnector`, which abstracts all Steam-specific data retrieval behind a platform-agnostic interface.
- **Enums** — `Platform` (Steam, Epic, GoG), `GroupRole` (Host, Member), `VoteSessionStatus` (Active, Closed).
- **Domain exceptions** — strongly typed exceptions (`DomainException`, `InvalidVoteException`) so callers can distinguish business rule violations from infrastructure faults.

This project has zero non-framework NuGet dependencies. Any code that references `Npgsql`, `HttpClient`, `ILogger`, or anything outside `System.*` does not belong here.

### PlayForge.Application

The application layer orchestrates domain objects and coordinates infrastructure:

- **Application services** — one service class per aggregate root (`UserService`, `GameGroupService`, `VoteSessionService`, `LibraryService`). Methods map to user actions: `SyncLibraryAsync`, `CreateGroupAsync`, `JoinGroupAsync`, `StartVoteSessionAsync`, `CastVoteAsync`.
- **DTOs** — plain data containers for moving data between layers and into Blazor components. No domain types are returned from service methods; everything is projected into a DTO at the service boundary.
- **Application interfaces** — `IUserRepository`, `IGameGroupRepository`, `IVoteSessionRepository`, `IGameRepository`. These are defined here so the Application layer can depend on the abstraction rather than the EF Core implementation.
- **PlatformConnectorRegistry** — resolves an `IPlatformConnector` by a `Platform` enum value. The Web layer and Application services call this registry; they never reference `SteamConnector` directly.

### PlayForge.Infrastructure

The infrastructure layer implements all interfaces defined in the Application layer and connects to external systems:

- **AppDbContext** — EF Core `DbContext` with entity configurations, value object mappings (`OwnsOne`), and the `uuid[]` column type for `VoteSession.CandidateGameIds`.
- **Repositories** — EF Core implementations of `IUserRepository`, `IGameGroupRepository`, and so on.
- **SteamApiClient** — typed `HttpClient` configured with `Microsoft.Extensions.Http.Resilience` (Polly) for retry and circuit-breaker. Wraps all Steam Web API and Steam Store API calls.
- **SteamConnector** — implements `IPlatformConnector` by delegating to `SteamApiClient`. Registered with `PlatformConnectorRegistry` under `Platform.Steam`.
- **Caching adapters** — wraps expensive Steam API responses and game metadata reads in `IMemoryCache`.

### PlayForge.Web

The web layer is the composition root and the only place that wires everything together:

- **Program.cs** — registers DI services, EF Core, authentication middleware, SignalR, health checks. Runs `MigrateAsync()` at startup.
- **Blazor Server pages** — Razor components under `Components/Pages/`. Components call application services through injected interfaces; they do not access the database or HTTP clients directly.
- **VoteHub** — SignalR hub that handles vote casting over a WebSocket circuit. Broadcasts tally updates to all group members connected at that moment.
- **Steam auth handler** — configures the `AspNet.Security.OpenId.Steam` middleware, handles the `/signin-steam` callback, creates or updates the `User` record after authentication.
- **Health endpoint** — `/health` backed by an EF Core DbContext health check. Returns 200 Healthy or 503 Unhealthy.

---

## Domain Model

### Entity Descriptions

**User**

Represents a PlayForge account linked to a Steam profile. Created on first sign-in. Holds `SteamId` (value object), `DisplayName`, `AvatarUrl`, and a navigation to their `UserGame` collection. The `SteamId` is immutable after creation; `DisplayName` and `AvatarUrl` are refreshed on each sign-in from `GetPlayerSummaries`.

**Game**

A game in the catalogue, identified by `AppId` (value object). Holds `Title`, `HeaderImageUrl`, `Tags` (string list), `ReviewScore`, and `ReviewCount`. Populated from the Steam Store API. One `Game` row is shared across all users who own it — `UserGame` is the per-user relationship.

**UserGame**

An explicit join entity between `User` and `Game`. Holds `PlaytimeMinutes` as pulled from `IPlayerService/GetOwnedGames`. Using an explicit entity rather than a many-to-many skip navigation preserves the ability to store per-user-per-game metadata and to query "games owned by all members of a group" efficiently with a `GROUP BY` + `COUNT` pattern.

**GameGroup**

A group of users with a shared candidate list and vote history. Holds `Name`, `InviteCode` (generated random string, unique), `HostUserId`, a `GroupMembership` collection, a `GroupCandidate` collection, and a `VoteSession` collection. The host can manage candidates and start vote sessions.

**GroupMembership**

Join entity between `User` and `GameGroup`. Holds `Role` (`Host` or `Member`). Every group has exactly one Host membership, created when the group is created.

**GroupCandidate**

A game nominated by any group member as a voting candidate. References both `GameGroup` and `Game`. Holds `NominatedByUserId` and a `NominatedAt` timestamp. Candidates persist across vote sessions; a host selects which candidates go into a specific session.

**VoteSession**

The most complex entity. Holds:
- `GameGroupId` — which group this session belongs to
- `Status` — `Active` or `Closed`
- `CandidateGameIds` — a `uuid[]` column (PostgreSQL native array) set when the session is started; write-once
- `StartedAt`, `ClosedAt` timestamps
- `WinnerGameId` — set when closed
- A collection of `Vote` entities

**Vote**

A single cast vote. Holds `VoteSessionId`, `UserId`, and `GameId`. The combination `(VoteSessionId, UserId)` has a unique constraint — one vote per user per session, enforced at both the domain and database level.

### Key Relationships

```
User ──< UserGame >── Game
User ──< GroupMembership >── GameGroup
GameGroup ──< GroupCandidate >── Game
GameGroup ──< VoteSession
VoteSession ──< Vote
Vote >── User
Vote >── Game
```

### Value Objects

**SteamId**

Wraps a `long`. Validates that the value is greater than zero on construction. Provides implicit conversion from `long` for convenience in entity factory methods. Two `SteamId` instances are equal if their underlying values are equal.

```
SteamId steamId = new SteamId(76561198000000000L);
```

**AppId**

Wraps a `long`. Same validation and equality semantics as `SteamId`. Kept as a separate type so the compiler prevents accidentally passing a SteamId where an AppId is expected.

---

## VoteSession State Machine

A `VoteSession` transitions through two states: `Active` and `Closed`. The diagram below describes the allowed transitions and the rules governing each.

```
            StartSession()
                 │
                 ▼
         ┌──────────────┐
         │    Active     │◄─── CastVote() (any group member, once each)
         └──────┬────────┘
                │
         CloseSession()
         (host only)
                │
                ▼
         ┌──────────────┐
         │    Closed     │  WinnerGameId set, ClosedAt stamped
         └──────────────┘
```

**Rules enforced by `VoteSession` domain methods:**

- `CastVote(userId, gameId)` — throws `InvalidVoteException` if: the session is not `Active`; the `gameId` is not in `CandidateGameIds`; the `userId` has already voted in this session.
- `CloseSession()` — tallies votes. The game with the highest vote count becomes the winner. If two or more games tie for first place, the winner is selected by randomly choosing among tied candidates (`Random.Shared.GetItems`). Sets `Status = Closed`, `ClosedAt = DateTime.UtcNow`, and `WinnerGameId`.
- `CandidateGameIds` is set in the `StartSession` factory method and is never mutated afterward. This makes the candidate list immutable for the lifetime of a vote session, preventing race conditions between candidate management and active voting.

---

## Platform Extensibility

### The IPlatformConnector Pattern

`IPlatformConnector` is defined in `PlayForge.Domain/Interfaces/`. It declares the operations needed to retrieve gaming data from any platform:

```csharp
public interface IPlatformConnector
{
    Platform Platform { get; }
    Task<UserProfileDto> GetProfileAsync(string externalUserId);
    Task<IReadOnlyList<OwnedGameDto>> GetOwnedGamesAsync(string externalUserId);
    Task<IReadOnlyList<FriendDto>> GetFriendsAsync(string externalUserId);
}
```

`SteamConnector` in `PlayForge.Infrastructure/Steam/` implements this interface. It is the only class that knows anything about Steam-specific API endpoints, JSON shapes, or authentication headers.

`PlatformConnectorRegistry` in `PlayForge.Application/` holds a dictionary keyed by `Platform` enum, populated at startup via DI registration:

```csharp
// In Program.cs / DI setup
services.AddSingleton<IPlatformConnector, SteamConnector>();
services.AddSingleton<PlatformConnectorRegistry>();
```

Application services resolve the correct connector at runtime:

```csharp
var connector = _registry.GetConnector(user.Platform);
var ownedGames = await connector.GetOwnedGamesAsync(user.ExternalId);
```

### Adding a New Platform (Epic, GOG)

To add GOG support, the steps are:

1. Add `GoG` to the `Platform` enum in `PlayForge.Domain`.
2. Create `GogConnector : IPlatformConnector` in `PlayForge.Infrastructure/GoG/`.
3. Register `GogConnector` in `Program.cs` — the registry picks it up automatically.
4. Add a GOG sign-in option to the login page.

No changes to the Application layer or any existing domain logic are required.

---

## Data Layer

### EF Core Mappings

Entity configurations live in `PlayForge.Infrastructure/Persistence/Configurations/`. Each entity has its own configuration class implementing `IEntityTypeConfiguration<T>`.

**Value objects with OwnsOne**

`SteamId` and `AppId` are stored as a single column using EF Core's `OwnsOne` fluent API:

```csharp
// UserConfiguration.cs
builder.OwnsOne(u => u.SteamId, owned =>
{
    owned.Property(s => s.Value)
         .HasColumnName("steam_id")
         .IsRequired();
});
```

This avoids a join table and keeps the column directly on the owning entity's row.

**CandidateGameIds as uuid[]**

PostgreSQL's native array type is used for `VoteSession.CandidateGameIds`:

```csharp
// VoteSessionConfiguration.cs
builder.Property(v => v.CandidateGameIds)
       .HasColumnType("uuid[]")
       .IsRequired();
```

Npgsql maps `Guid[]` on the C# side to `uuid[]` in Postgres. The array is written once when the session is started and never updated, so there is no concern about partial-update semantics.

**Explicit join entities**

`UserGame`, `GroupMembership`, and `GroupCandidate` are configured as explicit entities rather than many-to-many skip navigations. This allows EF to expose `PlaytimeMinutes`, `Role`, and `NominatedByUserId` as first-class properties, and it makes the "games owned by all group members" query straightforward:

```sql
SELECT g.game_id
FROM user_games g
WHERE g.user_id IN (/* group member ids */)
GROUP BY g.game_id
HAVING COUNT(DISTINCT g.user_id) = /* member count */;
```

**Auto-migrate on startup**

`Program.cs` calls `await dbContext.Database.MigrateAsync()` before the app begins accepting requests. This ensures the schema is always up to date, including on first deployment when the `playforge` database is empty. For production deployments that need more control, this call can be replaced with a separate migration step in the Docker entrypoint.

---

## Caching Strategy

### What Is Cached

| Data | Cache Key Pattern | TTL | Rationale |
|---|---|---|---|
| Steam player summary | `steam:profile:{steamId}` | 5 minutes | Called on every page load for the nav avatar; Steam rate-limits GetPlayerSummaries |
| Owned games list | `steam:library:{steamId}` | 10 minutes | Large payload; changes infrequently |
| Friends list | `steam:friends:{steamId}` | 5 minutes | Changes infrequently during a session |
| Game metadata (tags, review score) | `game:meta:{appId}` | 24 hours | Store API data is stable; only refresh needed on new game additions |

### What Is NOT Cached

Vote tallies are always read directly from the database. Because votes are cast in real time by multiple users simultaneously, a cached tally would produce stale counts and undermine the real-time guarantee. The vote count query is a simple `COUNT GROUP BY` on the `votes` table and is fast enough to run on every SignalR broadcast.

### IMemoryCache to IDistributedCache Migration Path

All cache operations go through `ICacheService`, an application-layer interface. The `MemoryCacheService` implementation wraps `IMemoryCache`. To switch to Redis for horizontal scaling:

1. Add a `RedisCacheService` implementation wrapping `IDistributedCache`.
2. Change one DI registration in `Program.cs`:

```csharp
// Before
services.AddSingleton<ICacheService, MemoryCacheService>();

// After
services.AddStackExchangeRedisCache(opts => opts.Configuration = "redis:6379");
services.AddSingleton<ICacheService, RedisCacheService>();
```

No other code changes are required.

---

## Real-Time Architecture

### SignalR Topology

PlayForge uses a single SignalR hub: `VoteHub`, located in `PlayForge.Web/Hubs/`. Because Blazor Server itself runs over SignalR circuits, both the Blazor circuit and the explicit `VoteHub` share the same in-process SignalR infrastructure. No external message bus is required for a single-node deployment.

**VoteHub**

Clients join a group-scoped SignalR group when they navigate to a group detail page:

```
Client connects → JoinGroupAsync(groupId) → added to SignalR group "group:{groupId}"
Client votes     → CastVoteAsync(sessionId, gameId) → vote written to DB → TallyUpdated broadcast to "group:{groupId}"
Host closes      → CloseSessionAsync(sessionId) → SessionClosed broadcast to "group:{groupId}"
```

**VoteNotifier singleton**

`VoteNotifier` is a singleton service injected into application services. It holds a reference to `IHubContext<VoteHub>` and exposes methods like `NotifyTallyUpdatedAsync(groupId, tally)` and `NotifySessionClosedAsync(groupId, winnerId)`. Application services call `VoteNotifier` rather than `IHubContext` directly, keeping the SignalR dependency out of the Application layer.

**Circuit-safe InvokeAsync pattern**

Blazor Server components run on a circuit. Updating component state from a background broadcast (like a SignalR message) requires marshalling back to the render thread:

```csharp
// Inside a Blazor component receiving a SignalR broadcast
await InvokeAsync(() =>
{
    _tally = updatedTally;
    StateHasChanged();
});
```

All component hub subscriptions follow this pattern. The `VoteHub` client-side handlers in Razor components are always written with `InvokeAsync(StateHasChanged)` to avoid cross-thread state mutations.

**Reconnect UI**

The Blazor Server host page includes a custom reconnect UI with three states:

- **Reconnecting** — shown immediately when the circuit drops; spinner with "Reconnecting..." message
- **Failed** — shown after all retry attempts fail; "Connection lost. Please reload." with a manual reload button
- **Session expired** — shown when the server has discarded the circuit state (typically after long inactivity); "Your session has expired. Please sign in again."

---

## Steam Integration

### API Endpoints and When They Are Called

| Endpoint | Called When | Notes |
|---|---|---|
| `ISteamUser/GetPlayerSummaries/v2` | Sign-in callback; each page load (cached 5 min) | Batch accepts up to 100 Steam IDs; used both for the signed-in user's avatar and for rendering friends lists |
| `IPlayerService/GetOwnedGames/v1` | User visits `/library`; background sync on sign-in | `include_appinfo=1` returns game names and icons inline, avoiding per-game detail calls |
| `ISteamUser/GetFriendList/v1` | User visits `/friends` | Returns a list of Steam IDs; then batched through `GetPlayerSummaries` to resolve display names |
| `store.steampowered.com/api/appdetails` | When a new game is added to the catalogue | Fetches tags, review score, screenshots. Rate-limited by Steam; called once per game and cached for 24 hours |
| `store.steampowered.com/api/storesearch` | Typeahead search when adding group candidates | Free-text search; returns up to 25 results; not cached (live search) |
| CDN `header.jpg` | Every game card render | No API key required; served from Akamai; URL constructed as `https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg` |

### Rate Limiting Awareness

Steam's Web API imposes a rate limit of approximately 100,000 requests per day per API key, with burst limits not publicly documented. PlayForge mitigates this through:

- **Caching** — library and profile responses are cached so repeated navigation does not re-call Steam.
- **Batch calls** — `GetPlayerSummaries` accepts up to 100 IDs per request; friends lists are batched accordingly.
- **Polly retry** — `SteamApiClient` is configured with `Microsoft.Extensions.Http.Resilience` using an exponential backoff with jitter for transient failures (429, 5xx). It does not retry 4xx errors other than 429.

---

## Security

### Steam OpenID Flow

1. The user clicks "Sign in through Steam" on the landing page.
2. The browser is redirected to `https://steamcommunity.com/openid/login` with a `return_to` parameter pointing to `https://your-domain/signin-steam`.
3. Steam authenticates the user and redirects them back to `/signin-steam` with OpenID assertion parameters.
4. `AspNet.Security.OpenId.Steam` middleware validates the OpenID assertion against Steam's endpoint. A forged redirect without a valid Steam signature will be rejected.
5. On successful validation, the middleware calls the `OnAuthenticated` event. PlayForge's handler extracts the Steam ID from the claimed identity, upserts the `User` record in the database, and issues an ASP.NET Core cookie.

### Cookie Authentication

After the OpenID handshake, PlayForge uses standard ASP.NET Core cookie authentication. The cookie is:

- HttpOnly (not accessible from JavaScript)
- Secure (sent only over HTTPS in production; Development mode may relax this)
- SameSite=Lax (protects against cross-site request forgery for most form submissions)

No JWT tokens are issued. All server-side session state lives in the Blazor Server circuit, which is bound to the authenticated cookie session.

### No Steam API Key in Client

The Steam API key is held exclusively in the server-side environment variable `STEAM_API_KEY`. It is never serialised into any client-side response, JavaScript bundle, or Blazor component. All Steam API calls originate from `SteamApiClient` running server-side.

### CSRF Protection

ASP.NET Core Antiforgery tokens protect any non-Blazor form endpoints (such as the sign-out endpoint). Blazor Server's own interactive components do not need explicit antiforgery tokens because they communicate over the SignalR circuit (not raw HTTP POST), and the circuit is already bound to an authenticated session.
