# PlayForge — Developer Setup Guide

## Prerequisites

Install the following tools before working on PlayForge.

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0 or later | `dotnet --version` must report `10.x.x` |
| Docker Desktop | 4.x or later | Required for Docker Compose and integration tests |
| Git | Any recent version | |
| PostgreSQL client | Any (optional) | `psql`, DBeaver, or TablePlus — useful for inspecting the database directly |
| EF Core CLI tools | 10.0 | `dotnet tool install --global dotnet-ef` |

Verify your .NET SDK version:

```bash
dotnet --version
# Expected: 10.0.xxx
```

Install the EF Core CLI tools globally if not already present:

```bash
dotnet tool install --global dotnet-ef
dotnet ef --version
# Expected: Entity Framework Core .NET Command-line Tools 10.x.x
```

---

## Clone and First Build

```bash
# Clone the repository
git clone https://github.com/your-org/playforge.git
cd playforge

# Restore NuGet packages and build the solution
dotnet build PlayForge.slnx
```

A clean build produces no errors or warnings. If you see a warning about `Microsoft.EntityFrameworkCore` version conflicts, verify that both `PlayForge.Infrastructure` and `PlayForge.Web` pin the same `Microsoft.EntityFrameworkCore` version in their `.csproj` files. See the [configuration reference](configuration.md) for the pinned package list.

---

## Running Locally with Docker Compose

Docker Compose is the recommended way to run PlayForge during development. It starts PostgreSQL, the PlayForge application, and nginx with a single command.

```bash
# Copy the environment file template
cp .env.example .env

# Edit .env — at minimum set STEAM_API_KEY and a POSTGRES_PASSWORD
# POSTGRES_USER=playforge
# POSTGRES_PASSWORD=yourpassword
# POSTGRES_DB=playforge
# STEAM_API_KEY=your_key_here

# Start all services
docker-compose up
```

The `docker-compose.override.yml` file applies development-specific overrides automatically when you run `docker-compose` without an explicit `-f` flag. It:

- Maps the source directory into the container so you can rebuild without recreating the image (useful with `dotnet watch` if you mount the source).
- Exposes PostgreSQL on `localhost:5432` so you can connect with a database client directly.
- Sets `ASPNETCORE_ENVIRONMENT=Development`, which enables the developer exception page and disables HTTPS redirection.

Once the stack is up, navigate to `http://localhost`. The nginx container proxies to the application on port 8080.

To stop the stack without losing database state:

```bash
docker-compose stop
```

To stop and remove containers (but keep the `postgres_data` volume):

```bash
docker-compose down
```

---

## Running Locally Without Docker

You can run the application directly against a local Postgres instance if you prefer faster iteration cycles than Docker rebuilds allow.

### Start a Local Postgres

The quickest way if Docker is available:

```bash
docker run -d \
  --name playforge-pg \
  -e POSTGRES_USER=playforge \
  -e POSTGRES_PASSWORD=devpassword \
  -e POSTGRES_DB=playforge \
  -p 5432:5432 \
  postgres:16
```

Or connect to an existing local Postgres installation.

### Configure the Connection String

Create or edit `src/PlayForge.Web/appsettings.Development.json`. This file is excluded from version control (`.gitignore`) and overrides `appsettings.json` in the Development environment:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=playforge;Username=playforge;Password=devpassword"
  },
  "Steam": {
    "ApiKey": "your_steam_api_key_here"
  }
}
```

### Run the Application

```bash
cd src/PlayForge.Web
dotnet run
```

The application starts on `http://localhost:5000` (HTTP) and `https://localhost:5001` (HTTPS) by default. Open `http://localhost:5000` in your browser.

EF Core will automatically apply any pending migrations on startup, creating the schema on first run.

---

## How Steam Login Works Locally

Steam OpenID is a server-to-server protocol. After the user approves the login on Steam's website, Steam redirects the user's browser to your `CallbackPath`, which is `/signin-steam`. Steam then validates the OpenID assertion by making a back-channel request from Steam's servers to your site's OpenID endpoint.

This means your local development URL (`http://localhost:5000`) must be reachable from the internet for Steam's back-channel validation to succeed.

### Option A: Use ngrok (recommended for local development)

[ngrok](https://ngrok.com) creates a public tunnel to your local machine.

```bash
# Install ngrok, then:
ngrok http 5000
# ngrok will print something like: https://a1b2c3d4.ngrok.io
```

Update your `appsettings.Development.json` to set the public base URL:

```json
{
  "AppBaseUrl": "https://a1b2c3d4.ngrok.io"
}
```

And ensure the `CallbackPath` in the Steam OpenID middleware configuration remains `/signin-steam`. The full callback URL that Steam will redirect to is `https://a1b2c3d4.ngrok.io/signin-steam`.

When you register your Steam API key at `https://steamcommunity.com/dev/apikey`, Steam asks for a "Domain Name". Set it to your ngrok subdomain (e.g., `a1b2c3d4.ngrok.io`). You will need to update this each time ngrok generates a new subdomain unless you have a paid ngrok plan with a fixed subdomain.

### Option B: Use a Real Domain

If you have a VPS or a domain you control, point it to your machine's public IP, open port 80 or 443, and run PlayForge there. This is more stable than ngrok but requires more setup for local-only development.

### The CallbackPath

The callback path is configured in `Program.cs` via the Steam OpenID middleware options. It defaults to `/signin-steam`. Do not change this path without also ensuring nginx (or whatever sits in front of the app) routes it correctly.

---

## EF Core Migrations

All migration commands must specify both the `--project` (where the `AppDbContext` lives) and the `--startup-project` (where `Program.cs` and the connection string configuration live).

### Add a New Migration

```bash
dotnet ef migrations add <MigrationName> \
  --project src/PlayForge.Infrastructure/PlayForge.Infrastructure.csproj \
  --startup-project src/PlayForge.Web/PlayForge.Web.csproj \
  --output-dir Persistence/Migrations
```

Replace `<MigrationName>` with a descriptive PascalCase name, for example `AddGroupInviteCode` or `AddVoteSessionStatusIndex`.

### Apply Migrations Manually

During development, migrations apply automatically on startup. To apply them manually (for example in a CI pipeline or if you disabled auto-migration):

```bash
dotnet ef database update \
  --project src/PlayForge.Infrastructure/PlayForge.Infrastructure.csproj \
  --startup-project src/PlayForge.Web/PlayForge.Web.csproj
```

### Revert the Last Migration

```bash
dotnet ef migrations remove \
  --project src/PlayForge.Infrastructure/PlayForge.Infrastructure.csproj \
  --startup-project src/PlayForge.Web/PlayForge.Web.csproj
```

This removes the most recently added migration file. Only works if that migration has not yet been applied to the database. If it has been applied, first roll back with:

```bash
dotnet ef database update <PreviousMigrationName> \
  --project src/PlayForge.Infrastructure/PlayForge.Infrastructure.csproj \
  --startup-project src/PlayForge.Web/PlayForge.Web.csproj
```

Then remove the migration file.

### List Applied Migrations

```bash
dotnet ef migrations list \
  --project src/PlayForge.Infrastructure/PlayForge.Infrastructure.csproj \
  --startup-project src/PlayForge.Web/PlayForge.Web.csproj
```

---

## Running Tests

The solution contains three test projects.

### Domain Tests

Pure unit tests with no external dependencies. Run without Docker.

```bash
dotnet test tests/PlayForge.Domain.Tests/PlayForge.Domain.Tests.csproj
```

These tests cover entity invariants (e.g., `VoteSession.CastVote` throws on a duplicate vote), value object equality and validation, and the tiebreak logic in `VoteSession.CloseSession`.

### Application Tests

Unit tests for application services using Moq to mock repositories and `IPlatformConnector`. Run without Docker.

```bash
dotnet test tests/PlayForge.Application.Tests/PlayForge.Application.Tests.csproj
```

These tests verify that services call the correct repository methods, handle connector responses correctly, and propagate domain exceptions appropriately.

### Integration Tests

Tests against a real PostgreSQL instance provisioned by Testcontainers. Docker must be running.

```bash
dotnet test tests/PlayForge.Integration.Tests/PlayForge.Integration.Tests.csproj
```

Testcontainers starts a fresh PostgreSQL 16 container for the test session and applies migrations before each test class. The tests verify EF Core queries, migration correctness, and repository implementations.

### Running All Tests

```bash
dotnet test PlayForge.slnx
```

---

## Project Structure Walkthrough

```
PlayForge.slnx                  # Solution file (new XML format, .NET 10)

src/
  PlayForge.Domain/
    Entities/                   # User, Game, UserGame, GameGroup, GroupMembership,
    │                           #   GroupCandidate, VoteSession, Vote
    ValueObjects/               # SteamId, AppId
    Interfaces/                 # IPlatformConnector, ICacheService (domain-facing subset)
    Enums/                      # Platform, GroupRole, VoteSessionStatus
    Exceptions/                 # DomainException, InvalidVoteException

  PlayForge.Application/
    Services/                   # UserService, LibraryService, GameGroupService,
    │                           #   VoteSessionService
    DTOs/                       # All data transfer objects (one per aggregate area)
    Interfaces/                 # IUserRepository, IGameGroupRepository, etc.
    PlatformConnectorRegistry.cs

  PlayForge.Infrastructure/
    Persistence/
      AppDbContext.cs
      Configurations/           # One IEntityTypeConfiguration<T> per entity
      Migrations/               # Auto-generated EF migration files
    Repositories/               # EF Core repository implementations
    Steam/
      SteamApiClient.cs         # Typed HttpClient for all Steam API calls
      SteamConnector.cs         # IPlatformConnector implementation
    Caching/
      MemoryCacheService.cs

  PlayForge.Web/
    Components/
      Pages/                    # Home.razor, Library.razor, Friends.razor,
      │                         #   Groups.razor, GroupDetail.razor, GroupHistory.razor
      Layout/                   # MainLayout.razor, NavMenu.razor
      Shared/                   # GameCard.razor, VoteTally.razor, ReconnectUI.razor
      _Imports.razor
    Hubs/
      VoteHub.cs
    Services/
      VoteNotifier.cs
    wwwroot/                    # CSS, images, favicon
    Program.cs                  # Composition root
    appsettings.json
    appsettings.Development.json  # Git-ignored; local overrides

tests/
  PlayForge.Domain.Tests/
  PlayForge.Application.Tests/
  PlayForge.Integration.Tests/

docker/
  nginx/nginx.conf
  postgres/init.sql             # Runs once on first DB creation (not migrations)

docker-compose.yml
docker-compose.override.yml     # Development overrides (auto-applied locally)
.env.example                    # Template — copy to .env and fill in values
```

---

## Key Files to Know

### `src/PlayForge.Web/Program.cs`

The composition root. Registers all services, middleware, and configuration. The order of middleware registration matters for ASP.NET Core — authentication must come before authorisation, and UseRouting must come before UseEndpoints. Auto-migration runs here before `app.Run()`.

### `src/PlayForge.Infrastructure/Persistence/AppDbContext.cs`

Defines all `DbSet<T>` properties and calls `ApplyConfigurationsFromAssembly` to pick up all entity configurations automatically. Adding a new entity requires a `DbSet`, a configuration class in `Configurations/`, and a new migration.

### `src/PlayForge.Infrastructure/Steam/SteamApiClient.cs`

The only class that constructs Steam API request URLs and parses Steam API JSON responses. If a Steam API endpoint changes or a new one is needed, this is the only file to touch. Uses `System.Text.Json` with camelCase property naming to match Steam's response format.

### `src/PlayForge.Domain/Entities/VoteSession.cs`

The most complex entity. Contains the `CastVote`, `CloseSession`, and `StartSession` factory method. The tiebreak logic lives in `CloseSession`. Read this file to understand the domain invariants before changing anything vote-related.

### `src/PlayForge.Web/Components/Pages/GroupDetail.razor`

The most complex Blazor component. It manages the SignalR hub connection lifecycle, handles vote casting, renders the live tally, manages the reconnect UI states, and conditionally shows host-only controls. Understanding the component lifecycle (`OnInitializedAsync`, `DisposeAsync`, `InvokeAsync`) is important before editing this file.

---

## Code Conventions

### Nullable Reference Types

`<Nullable>enable</Nullable>` is set in all project files. All reference type parameters and return types must be explicitly annotated with `?` where null is a valid value. Methods that can return null must be typed as `T?`. Suppressing nullable warnings with `!` is discouraged; prefer a null check or a guard clause.

### Implicit Usings

`<ImplicitUsings>enable</ImplicitUsings>` is set in all project files. You do not need to add `using System;`, `using System.Collections.Generic;`, and so on. Domain-specific namespaces must still be explicitly imported.

### Private Setters on Entities

All entity properties have `private set;` or `private init;`. The only way to change entity state is through domain methods (`CastVote`, `CloseSession`, `AddMember`, etc.). This prevents application services or Blazor components from accidentally bypassing business rules by setting properties directly.

```csharp
// Correct — property is private
public string DisplayName { get; private set; }

// Correct — state change goes through a domain method
public void UpdateProfile(string displayName, string avatarUrl)
{
    if (string.IsNullOrWhiteSpace(displayName))
        throw new DomainException("Display name cannot be blank.");

    DisplayName = displayName;
    AvatarUrl = avatarUrl;
}
```

### Factory Methods

Entities expose static factory methods instead of public constructors. This keeps construction logic in the domain and gives descriptive names to the creation intent:

```csharp
// Usage
var session = VoteSession.Start(groupId, candidateGameIds, startedByUserId);

// Not: new VoteSession(groupId, candidateGameIds, startedByUserId)
```

EF Core requires a private or protected parameterless constructor to hydrate entities from the database. Add one marked with a `// EF Core constructor` comment so reviewers know it is intentional.

### Naming

- Async methods end in `Async` and return `Task` or `Task<T>`.
- Application service methods use verb-noun names: `SyncLibraryAsync`, `CreateGroupAsync`, `CastVoteAsync`.
- Repository methods use data-access names: `GetByIdAsync`, `GetByGroupIdAsync`, `AddAsync`, `SaveChangesAsync`.
- Blazor component files use PascalCase matching the page route noun: `Library.razor`, `GroupDetail.razor`.
