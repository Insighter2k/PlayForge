# PlayForge — Configuration Reference

## .env File Reference

The `.env` file sits in the repository root and is loaded by Docker Compose at startup. It is the primary configuration surface for a production deployment. Copy `.env.example` to `.env` and fill in values before running `docker compose up`.

The `.env` file is listed in `.gitignore` and must never be committed to version control.

| Variable | Description | Default | Required |
|---|---|---|---|
| `POSTGRES_USER` | PostgreSQL superuser name for the `playforge` database | `playforge` | Yes |
| `POSTGRES_PASSWORD` | Password for `POSTGRES_USER`. Use a strong, randomly generated string in production. | (none) | Yes |
| `POSTGRES_DB` | Name of the PostgreSQL database that PlayForge will use | `playforge` | Yes |
| `STEAM_API_KEY` | Your Steam Web API key, obtained from `steamcommunity.com/dev/apikey`. Used server-side only — never exposed to clients. | (none) | Yes |

Example `.env` file:

```dotenv
POSTGRES_USER=playforge
POSTGRES_PASSWORD=change_this_to_a_strong_random_value
POSTGRES_DB=playforge
STEAM_API_KEY=ABCDEF1234567890ABCDEF1234567890
```

Docker Compose automatically injects these values into the containers that reference them via the `environment:` or `env_file:` keys in `docker-compose.yml`.

---

## appsettings.json Sections

The `src/PlayForge.Web/appsettings.json` file contains the base configuration for the application. Most values have production-safe defaults. Sensitive values (API keys, passwords) must be supplied via environment variables, which ASP.NET Core's configuration system merges over the JSON values automatically.

### Full structure

```json
{
  "ConnectionStrings": {
    "Default": "Host=postgres;Port=5432;Database=playforge;Username=playforge;Password=changeme"
  },
  "Steam": {
    "ApiKey": "",
    "CallbackPath": "/signin-steam"
  },
  "Caching": {
    "ProfileTtlMinutes": 5,
    "LibraryTtlMinutes": 10,
    "FriendsTtlMinutes": 5,
    "GameMetaTtlHours": 24
  },
  "HealthChecks": {
    "Path": "/health"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

In Docker Compose, `appsettings.json` is baked into the image. Environment variables in the container override individual keys using ASP.NET Core's double-underscore convention:

```
ConnectionStrings__Default=Host=postgres;Port=5432;...
Steam__ApiKey=your_key_here
```

The `docker-compose.yml` maps `.env` values to these environment variables for the `app` container:

```yaml
app:
  environment:
    - ConnectionStrings__Default=Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
    - Steam__ApiKey=${STEAM_API_KEY}
    - ASPNETCORE_ENVIRONMENT=Production
```

---

## Connection String Format

PlayForge uses [Npgsql](https://www.npgsql.org) to connect to PostgreSQL. The connection string follows Npgsql's keyword/value format.

### Full Example

```
Host=postgres;Port=5432;Database=playforge;Username=playforge;Password=your_password;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=20;Connection Idle Lifetime=300
```

### Parameter Reference

| Parameter | Description | Default |
|---|---|---|
| `Host` | Hostname or IP of the PostgreSQL server. Inside Docker Compose, use the service name `postgres`. For local development without Docker, use `localhost`. | (required) |
| `Port` | PostgreSQL port | `5432` |
| `Database` | Database name | (required) |
| `Username` | PostgreSQL user | (required) |
| `Password` | PostgreSQL user password | (required) |
| `Pooling` | Enable connection pooling | `true` |
| `Minimum Pool Size` | Minimum connections kept open | `1` |
| `Maximum Pool Size` | Maximum concurrent connections | `100` (Npgsql default) — reduce to `20` for small servers |
| `Connection Idle Lifetime` | Seconds before an idle connection is closed (seconds) | `300` |
| `SSL Mode` | `Disable`, `Prefer`, or `Require`. Set to `Require` if your Postgres server has TLS enabled. | `Prefer` |

For local development, a minimal connection string is sufficient:

```
Host=localhost;Database=playforge;Username=playforge;Password=devpassword
```

---

## Steam Section

```json
"Steam": {
  "ApiKey": "",
  "CallbackPath": "/signin-steam"
}
```

| Key | Description |
|---|---|
| `ApiKey` | Your Steam Web API key. Supply via environment variable (`Steam__ApiKey`) in production, never hardcoded in `appsettings.json`. |
| `CallbackPath` | The path Steam redirects to after OpenID authentication. Changing this requires updating the `AspNet.Security.OpenId.Steam` middleware configuration in `Program.cs` to match. The default `/signin-steam` is the library's default and should not need to change. |

The `ApiKey` value from this section is injected into `SteamApiClient` via the options pattern. It is read once at startup and held in memory server-side. It is never written to any HTTP response.

---

## Caching TTLs

Cache time-to-live values are configured in the `Caching` section of `appsettings.json`. All durations are read at startup and passed to `MemoryCacheService` via `IOptions<CachingOptions>`.

```json
"Caching": {
  "ProfileTtlMinutes": 5,
  "LibraryTtlMinutes": 10,
  "FriendsTtlMinutes": 5,
  "GameMetaTtlHours": 24
}
```

| Key | Applies To | Default | Notes |
|---|---|---|---|
| `ProfileTtlMinutes` | Steam player summary (`GetPlayerSummaries`) | `5` minutes | Called on page load to populate the nav avatar and display name. Shorter TTL keeps the avatar fresher. |
| `LibraryTtlMinutes` | Owned games list (`GetOwnedGames`) | `10` minutes | Reduces Steam API calls during a session where the user revisits `/library`. |
| `FriendsTtlMinutes` | Friends list (`GetFriendList`) | `5` minutes | Friends lists change infrequently; this balances freshness with API call reduction. |
| `GameMetaTtlHours` | Game metadata from Steam Store API (`appdetails`) | `24` hours | Tags, review scores, and screenshots change rarely. A 24-hour cache keeps data fresh daily while avoiding repeated Store API calls. |

To change a TTL, update `appsettings.json` and redeploy. No code change is needed. Setting a value to `0` effectively disables caching for that data type (every request goes to Steam), which is useful for debugging caching behaviour but should not be used in production.

The caching values live in the `Caching` section rather than being hard-coded so that operators can tune them without rebuilding the Docker image when running Docker Compose with a volume-mounted config file, or by supplying environment variable overrides (e.g., `Caching__LibraryTtlMinutes=15`).

---

## ASP.NET Core Environment Modes

The `ASPNETCORE_ENVIRONMENT` environment variable controls which `appsettings.{Environment}.json` file is layered on top of the base `appsettings.json`, and which ASP.NET Core features are enabled.

### Development

Set automatically by `docker-compose.override.yml` for local development:

```
ASPNETCORE_ENVIRONMENT=Development
```

Behaviour differences in Development mode:

- **Developer Exception Page** — Unhandled exceptions display a full stack trace in the browser. Do not run in Development mode on a public server.
- **Detailed error messages** — ASP.NET Core logs more verbosely. EF Core logs all SQL queries (enable by setting `Microsoft.EntityFrameworkCore: Information` in the Logging section).
- **HTTPS Redirection disabled** — The `UseHttpsRedirection()` middleware is conditionally skipped in Development to avoid certificate issues when running over plain HTTP locally.
- **appsettings.Development.json loaded** — Any keys in this file override `appsettings.json`. This file is git-ignored and is the correct place for local secrets and connection strings.

### Production

Set in `docker-compose.yml` for production:

```
ASPNETCORE_ENVIRONMENT=Production
```

Behaviour differences in Production mode:

- **Generic error page** — Unhandled exceptions return a generic 500 page with no stack trace.
- **HTTPS Redirection enabled** — HTTP requests are redirected to HTTPS (ensure your reverse proxy or TLS terminator is configured before enabling this, otherwise you may create redirect loops).
- **Stricter cookie settings** — The auth cookie uses `Secure = true` and `SameSite = Lax`.
- **Reduced logging verbosity** — Only `Warning` and above from `Microsoft.AspNetCore` and EF Core namespaces are logged. Application-level `Information` logs are still emitted.

---

## Health Endpoint Configuration

The health endpoint is registered in `Program.cs` via ASP.NET Core Health Checks:

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

app.MapHealthChecks("/health");
```

The path `/health` is the default and matches the `HealthChecks.Path` setting in `appsettings.json`. If you change the path, update both `appsettings.json` and the `MapHealthChecks` call in `Program.cs`.

**Response format:**

```http
GET /health HTTP/1.1

HTTP/1.1 200 OK
Content-Type: application/json

{
  "status": "Healthy",
  "totalDuration": "00:00:00.0123",
  "entries": {
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.0115",
      "tags": []
    }
  }
}
```

**HTTP status codes:**

| Status | HTTP Code | Meaning |
|---|---|---|
| `Healthy` | 200 | All checks passed |
| `Degraded` | 200 | Checks passed but with warnings (not currently configured) |
| `Unhealthy` | 503 | One or more checks failed — database is unreachable |

The health endpoint is unauthenticated and does not require a Steam login. It is designed to be polled by Docker health checks, uptime monitors (e.g., UptimeRobot, Uptime Kuma), and load balancers.

The nginx configuration does not restrict access to `/health`. If you want to limit health check access to internal networks, add an `allow`/`deny` block in `nginx.conf`:

```nginx
location /health {
    allow 10.0.0.0/8;
    allow 172.16.0.0/12;
    deny all;
    proxy_pass http://app:8080;
}
```

---

## SignalR and Blazor Circuit Timeout Notes

Blazor Server and the `VoteHub` both depend on persistent WebSocket connections. Several configuration values interact to determine how long connections are kept alive and what happens when they drop.

### nginx proxy_read_timeout

Located in `docker/nginx/nginx.conf`. Controls how long nginx waits for a response from the upstream (the `app` container) before closing the connection. For WebSocket connections, this acts as an idle timeout.

```nginx
proxy_read_timeout 86400;   # 86400 seconds = 24 hours
```

This value must be set to a large number for both `/_blazor` (Blazor circuit) and `/hubs/` (VoteHub) locations. The default nginx `proxy_read_timeout` is 60 seconds, which is far too short for a Blazor Server application — users would see the circuit disconnect every minute of inactivity.

### Blazor Server Circuit Disconnect Timeout

ASP.NET Core Blazor Server keeps a disconnected circuit alive in memory for a configurable period in case the client reconnects (e.g., after a brief network drop). Configured in `Program.cs`:

```csharp
builder.Services.AddServerSideBlazor(options =>
{
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
});
```

The default is 3 minutes. During this window, the user's page state (component tree, in-memory data) is held on the server. If the user reconnects within this period, they resume from where they were. If they do not reconnect in time, the circuit is discarded and the user will see the "Session expired" reconnect UI.

Increase this value if your users frequently experience short network interruptions (mobile connections, VPNs). Decrease it if memory usage is a concern on low-RAM servers (each retained circuit consumes server memory).

### SignalR Client Reconnect Policy

The Blazor JavaScript client and the `VoteHub` JavaScript client both have a reconnect policy that controls how many times and at what intervals they attempt to reconnect after a dropped connection.

The default Blazor reconnect policy retries 3 times (at 0, 2, and 10 seconds). For `VoteHub`, the reconnect policy in the Razor component's JavaScript interop attempts reconnect with increasing delays. These are configured in the component's JavaScript glue code and in Blazor's `_Host.cshtml` (or the equivalent Blazor host page).

The practical implication: if nginx drops a WebSocket connection due to an incorrect `proxy_read_timeout`, the client will attempt to reconnect. A timeout of 60 seconds means users will see a reconnect attempt every minute. Setting `proxy_read_timeout 86400` prevents this.
