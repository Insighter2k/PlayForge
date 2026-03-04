# PlayForge

A self-hosted web app for Steam gaming groups — vote together on what to play next.

## What is PlayForge?

PlayForge is an open-source, self-hosted companion app for groups of friends who game together on Steam. After signing in with their Steam account, each user can view their own game library, see which Steam friends have also joined PlayForge, form private gaming groups, and run real-time voting sessions to settle the perennial question: "What should we play tonight?"

Everything runs inside Docker on hardware you control. No accounts, no telemetry, no third-party service other than Steam's own public API.

## Features

- **Steam Library Sync** — Pull your full Steam library with playtime data and cover art directly from the Steam API. Filter by tag, sort by playtime, alphabetical order, or review score.
- **Friends Cross-Reference** — See which of your Steam friends have registered on the same PlayForge instance, making it easy to find your usual group.
- **Gaming Groups** — Create a group or join one with an invite code. Groups maintain a persistent roster and a candidate game list.
- **Common Games Intersection** — A group automatically surfaces games that every member owns, so candidates are always playable by everyone.
- **Real-Time Voting** — Start a vote session on a group's candidate list. Votes are tallied live via SignalR — no page refresh needed. Ties are broken randomly by default.
- **Session History** — Browse closed vote sessions with the winning game's cover art and vote counts.

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Blazor Server (.NET 10) |
| Backend | ASP.NET Core (.NET 10), co-hosted with Blazor |
| Database | PostgreSQL 16 + EF Core 10 |
| Real-time | SignalR (in-process, no Redis required for single-node) |
| Auth | Steam OpenID 2.0 via `AspNet.Security.OpenId.Steam` |
| Deployment | Docker Compose (app + postgres + nginx) |

## Quick Start

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose plugin on Linux)
- A Steam API key — get one at [https://steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey)
- A domain or hostname reachable from the internet (Steam OpenID requires a public callback URL)

### Steps

```bash
# 1. Clone the repository
git clone https://github.com/your-org/playforge.git
cd playforge

# 2. Copy the example environment file and fill in your values
cp .env.example .env
# Open .env in your editor and set:
#   POSTGRES_PASSWORD — choose a strong password
#   STEAM_API_KEY     — your key from steamcommunity.com/dev/apikey

# 3. Start the stack
docker-compose up -d

# 4. Open a browser
open http://localhost
# or navigate to http://your-server-ip
```

On first start, EF Core automatically runs any pending migrations, so no manual database setup is required.

### Signing In

Click "Sign in through Steam" on the landing page. Steam will redirect you back to PlayForge after authentication. On your first sign-in, a user profile is created automatically.

> **Note:** Steam OpenID redirects to `your-domain/signin-steam`. This URL must be reachable from Steam's servers. If you are running locally for development, see [docs/development.md](docs/development.md) for the ngrok tunnel approach.

## Documentation

| Document | Contents |
|---|---|
| [docs/architecture.md](docs/architecture.md) | Layer responsibilities, domain model, data layer, real-time, security |
| [docs/development.md](docs/development.md) | Local setup, EF migrations, running tests, code conventions |
| [docs/self-hosting.md](docs/self-hosting.md) | Production deployment, HTTPS/TLS, backup and restore, troubleshooting |
| [docs/configuration.md](docs/configuration.md) | All environment variables and appsettings reference |

## Known Limitations

### Game Cover Art

Cover images are loaded directly from Steam's public CDN (`cdn.akamai.steamstatic.com`) using each game's AppId. No images are stored locally.

A few situations can result in a blank cover slot:

- **No image on the CDN** — Older, obscure, or delisted games sometimes have no `header.jpg` at the expected URL. This is a Steam-side gap and cannot be worked around without a third-party image source.
- **Transient network error** — PlayForge will automatically retry a failed image up to twice (after 1 s and 2 s) before showing a placeholder. This covers brief CDN hiccups without any action needed.
- **Private Steam profile** — If a user's Steam profile is set to private, the friend list and some library data may be unavailable.

## Screenshots

> Screenshots will be added once the UI stabilises. See the feature list above for a description of each page.

## Support

If you find PlayForge useful, consider buying me a coffee.

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/insight2k)
