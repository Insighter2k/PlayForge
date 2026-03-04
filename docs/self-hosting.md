# PlayForge — Self-Hosting Guide

## Prerequisites

Before deploying PlayForge on your server, ensure the following are in place.

**Server requirements:**

- A Linux server (Ubuntu 22.04 LTS or Debian 12 recommended) with at least 1 GB RAM and 10 GB disk space
- Docker Engine and the Docker Compose plugin installed (instructions: [https://docs.docker.com/engine/install/](https://docs.docker.com/engine/install/))
- Ports 80 and 443 open in your server firewall and any upstream network firewall (cloud security groups, etc.)
- A domain name pointed at your server's public IP address (required for Steam OpenID; Steam will not redirect to a bare IP address)

**Accounts and keys:**

- A Steam API key — register at [https://steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey)
- Control over DNS for your domain so you can point it at the server

Verify Docker and Docker Compose are installed and working:

```bash
docker --version
# Docker version 26.x.x

docker compose version
# Docker Compose version v2.x.x
```

---

## Step-by-Step Deployment

### Step 1 — Clone the Repository

```bash
# SSH into your server, then:
git clone https://github.com/your-org/playforge.git
cd playforge
```

### Step 2 — Configure Environment Variables

Copy the example environment file and edit it with your values:

```bash
cp .env.example .env
nano .env   # or use vim, micro, or any editor you prefer
```

Fill in all required values. See the [configuration reference](configuration.md) for the full list of variables and their meaning. At a minimum you must set:

```
POSTGRES_PASSWORD=a_strong_random_password
STEAM_API_KEY=your_key_from_steamcommunity_com
```

Do not commit `.env` to version control. It is listed in `.gitignore` by default.

### Step 3 — Get a Steam API Key

If you do not already have a Steam API key:

1. Go to [https://steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey).
2. Sign in with your Steam account.
3. Enter your domain name (e.g., `playforge.example.com`) in the "Domain Name" field.
4. Agree to the Steam API Terms of Use.
5. Copy the generated key and paste it into your `.env` as `STEAM_API_KEY`.

You can use a single API key for the whole PlayForge instance regardless of how many users sign in.

### Step 4 — Register the Steam OpenID Callback URL

Steam OpenID requires that the callback URL your site uses is associated with the domain registered for your API key. The callback URL that PlayForge uses is:

```
https://your-domain.com/signin-steam
```

Ensure the domain you registered in Step 3 matches the domain where PlayForge is hosted. Steam's back-channel validation will contact `https://your-domain.com/signin-steam` after a user approves login. If the domain does not match, authentication will fail with an OpenID verification error.

No additional configuration is needed in the PlayForge application itself — `CallbackPath=/signin-steam` is the default.

### Step 5 — Start the Stack

```bash
docker compose up -d
```

This command:

1. Pulls the PostgreSQL 16 and nginx images if not already cached.
2. Builds the PlayForge application image from the multi-stage Dockerfile.
3. Starts all three containers: `postgres`, `app`, and `nginx`.
4. On first start, waits for Postgres to become healthy (the `app` container has a health-check dependency), then runs EF Core migrations to create the database schema.

To watch the startup logs:

```bash
docker compose logs -f
```

Wait until you see a line like `Application started. Press Ctrl+C to shut down.` from the `app` container.

### Step 6 — Verify the Deployment

Open a browser and navigate to `http://your-domain.com`. You should see the PlayForge landing page with a "Sign in through Steam" button.

Check the health endpoint:

```bash
curl http://your-domain.com/health
# Expected: Healthy
```

A `Healthy` response confirms the application started successfully and the database connection is working.

---

## Verifying the Deployment

### Health Endpoint

`GET /health` returns a JSON body from ASP.NET Core Health Checks:

```json
{"status":"Healthy","totalDuration":"00:00:00.012345","entries":{"database":{"status":"Healthy","duration":"00:00:00.011"}}}
```

A 200 status with `"status":"Healthy"` means:

- The application process is running.
- EF Core can successfully query the database.

A 503 with `"status":"Unhealthy"` means the database connection is failing. Check the `postgres` container logs.

### Application Logs

```bash
# Tail live logs from the application container
docker compose logs -f app

# View the last 100 lines from nginx
docker compose logs --tail=100 nginx

# View logs from postgres
docker compose logs postgres
```

---

## nginx Reverse Proxy

The nginx configuration is included in `docker/nginx/nginx.conf` and is mounted into the `nginx` container automatically by `docker-compose.yml`. You do not need to modify nginx for a basic HTTP deployment.

**Why nginx is required for Blazor Server:**

Blazor Server communicates over a persistent WebSocket connection to maintain the SignalR circuit. Without the correct nginx proxy headers, the WebSocket upgrade handshake will fail, the browser will fall back to long-polling, and eventually the Blazor circuit will disconnect.

The nginx configuration includes:

```nginx
# For the Blazor SignalR endpoint
location /_blazor {
    proxy_pass         http://app:8080;
    proxy_http_version 1.1;
    proxy_set_header   Upgrade $http_upgrade;
    proxy_set_header   Connection "upgrade";
    proxy_read_timeout 86400;
}

# For any additional SignalR hubs (e.g., VoteHub)
location /hubs/ {
    proxy_pass         http://app:8080;
    proxy_http_version 1.1;
    proxy_set_header   Upgrade $http_upgrade;
    proxy_set_header   Connection "upgrade";
    proxy_read_timeout 86400;
}
```

`proxy_read_timeout 86400` sets the timeout to 24 hours. This keeps the WebSocket connection alive even when the user's tab is open but idle. Without this, nginx will close idle connections after its default 60-second read timeout, causing Blazor circuit disconnections.

---

## HTTPS / TLS Options

HTTPS is required for production use. Steam OpenID will redirect to your callback URL over HTTPS, and browsers will warn users if your site is served over plain HTTP.

### Option A — Let's Encrypt with Certbot (Recommended)

Certbot issues free, automatically renewing TLS certificates via the ACME protocol.

**Install Certbot:**

```bash
sudo apt update
sudo apt install certbot python3-certbot-nginx -y
```

**Stop the nginx container temporarily** (Certbot needs port 80 for the ACME challenge):

```bash
docker compose stop nginx
```

**Obtain a certificate:**

```bash
sudo certbot certonly --standalone -d your-domain.com
```

Certbot will place certificates in `/etc/letsencrypt/live/your-domain.com/`.

**Update nginx.conf** to add an HTTPS server block:

```nginx
server {
    listen 443 ssl;
    server_name your-domain.com;

    ssl_certificate     /etc/letsencrypt/live/your-domain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/your-domain.com/privkey.pem;

    # ... same proxy_pass and WebSocket headers as above
}

server {
    listen 80;
    server_name your-domain.com;
    return 301 https://$host$request_uri;
}
```

Mount the Let's Encrypt directory into the nginx container by adding a volume to `docker-compose.yml`:

```yaml
nginx:
  volumes:
    - ./docker/nginx/nginx.conf:/etc/nginx/nginx.conf:ro
    - /etc/letsencrypt:/etc/letsencrypt:ro
```

**Restart the stack:**

```bash
docker compose up -d
```

**Set up auto-renewal.** Certbot installs a systemd timer by default. Verify it:

```bash
sudo systemctl status certbot.timer
```

Add a renewal hook to reload nginx after renewal:

```bash
echo "docker compose -f /path/to/playforge/docker-compose.yml exec nginx nginx -s reload" \
  | sudo tee /etc/letsencrypt/renewal-hooks/post/reload-nginx.sh
sudo chmod +x /etc/letsencrypt/renewal-hooks/post/reload-nginx.sh
```

### Option B — Cloudflare Proxy (Simplest)

If your domain is managed by Cloudflare:

1. In Cloudflare DNS, add an A record pointing your domain to your server's IP and set the proxy status to "Proxied" (orange cloud).
2. In Cloudflare SSL/TLS settings, set encryption mode to "Flexible" (Cloudflare handles HTTPS to the visitor; HTTP to your server) or "Full" (HTTPS to both, requires a self-signed cert on your server).
3. No changes to nginx or the PlayForge configuration are required.

Cloudflare's proxy terminates TLS, so visitors connect over HTTPS and Cloudflare forwards traffic to your server over HTTP. PlayForge's nginx container continues to listen on port 80.

Note: Cloudflare's WebSocket proxying works transparently on paid plans and on most configurations, but verify that your Cloudflare plan supports WebSocket passthrough. Blazor Server requires WebSocket; long-polling will work but degrades the experience.

### Option C — Existing Reverse Proxy

If you already run nginx, Caddy, Traefik, or Apache on the host, you can proxy from your existing reverse proxy to the PlayForge nginx container on port 80, or directly to the app container on port 8080 (if you expose it via `docker-compose.override.yml`).

Ensure your existing reverse proxy passes the WebSocket upgrade headers as shown in the nginx section above. The critical headers are:

```
Upgrade: $http_upgrade
Connection: "upgrade"
proxy_http_version 1.1 (nginx) or equivalent
```

---

## Updating to a New Version

```bash
# Pull the latest code
git pull origin main

# Rebuild the application image
docker compose build app

# Restart the stack — Compose will restart only changed containers
docker compose up -d

# Verify the update
curl http://your-domain.com/health
docker compose logs -f app
```

EF Core runs any new migrations automatically on startup. If a migration fails (for example due to a data conflict), the application will not start and will log the error. In that case, connect to the database with `psql` and resolve the conflict manually before restarting.

---

## Backup and Restore

PostgreSQL data is stored in the Docker named volume `postgres_data`. This volume persists across `docker compose down` and container restarts but would be lost if you explicitly delete it with `docker volume rm`.

### Create a Backup

```bash
# Run pg_dump inside the postgres container, writing to stdout
docker compose exec postgres pg_dump \
  -U playforge \
  -d playforge \
  --format=custom \
  > playforge_backup_$(date +%Y%m%d_%H%M%S).pgdump
```

This creates a binary-format dump file in your current directory. The `--format=custom` format is compressed and supports selective restore.

### Automate Daily Backups

Create a cron job on the host:

```bash
crontab -e
```

Add:

```
0 3 * * * cd /path/to/playforge && docker compose exec -T postgres pg_dump -U playforge -d playforge --format=custom > /var/backups/playforge/playforge_$(date +\%Y\%m\%d).pgdump 2>&1
```

Ensure `/var/backups/playforge/` exists and is writable. Add a second cron job or a `find` command to delete backups older than 30 days.

### Restore from Backup

Stop the application container so no writes occur during restore:

```bash
docker compose stop app
```

Drop and recreate the database:

```bash
docker compose exec postgres psql -U playforge -c "DROP DATABASE playforge;"
docker compose exec postgres psql -U playforge -c "CREATE DATABASE playforge;"
```

Restore from the dump file:

```bash
cat playforge_backup_20260101_030000.pgdump | \
  docker compose exec -T postgres pg_restore \
  -U playforge \
  -d playforge \
  --no-owner \
  --no-privileges
```

Restart the stack:

```bash
docker compose up -d
```

EF Core will run any migrations that are newer than the backup's schema state on startup. This is safe — migrations are idempotent.

---

## Troubleshooting

### Steam OpenID Callback Fails

**Symptom:** Clicking "Sign in through Steam" redirects to Steam, but after approving the login you are sent back to an error page or redirected to the home page without being signed in. The `app` container logs show an OpenID validation failure.

**Causes and fixes:**

- **Domain mismatch** — The domain in your Steam API key registration (`steamcommunity.com/dev/apikey`) does not match the domain PlayForge is running on. Update the API key domain to match.
- **HTTP vs HTTPS mismatch** — Steam expects to reach the callback at the HTTPS version of your URL. If you are running over HTTP only, this will fail. Set up HTTPS (see the TLS options above).
- **Firewall blocking port 443** — Steam's back-channel validation calls your server. Ensure port 443 is open inbound.
- **Clock skew** — OpenID assertions are time-sensitive. Ensure your server's system clock is accurate (most Linux distributions sync via NTP automatically; verify with `timedatectl`).

### Database Not Ready on First Start

**Symptom:** On the very first `docker compose up`, the `app` container exits with a database connection error before Postgres has fully initialised.

**Fix:** The `docker-compose.yml` `app` service has a `depends_on` with a `condition: service_healthy` on the `postgres` service. If this fails, it usually means the Postgres health check has not been configured correctly. Verify the health check in `docker-compose.yml`:

```yaml
postgres:
  healthcheck:
    test: ["CMD-SHELL", "pg_isready -U playforge -d playforge"]
    interval: 5s
    timeout: 5s
    retries: 10
```

If the `app` container still starts before Postgres is ready, increase `retries` or `interval`. As a fallback, start Postgres first, wait for it to be healthy, then start the rest:

```bash
docker compose up -d postgres
docker compose up -d   # starts the remaining services after postgres is healthy
```

### WebSocket Disconnections (Blazor Circuit Drops)

**Symptom:** After some time, the Blazor UI shows the "Reconnecting..." banner and either reconnects slowly or shows "Connection lost." Users on the vote page stop receiving live tally updates.

**Causes and fixes:**

- **Missing nginx WebSocket headers** — Verify that `nginx.conf` includes `proxy_http_version 1.1`, `Upgrade`, `Connection "upgrade"`, and `proxy_read_timeout 86400` for both `/_blazor` and `/hubs/` locations. Without these, nginx drops idle WebSocket connections after 60 seconds.
- **Cloudflare proxy timeout** — Cloudflare has a 100-second no-response timeout for HTTP requests. WebSocket connections over Cloudflare can be dropped on idle. Consider disabling Cloudflare proxy for the WebSocket paths or upgrading to a Cloudflare plan that supports longer WebSocket sessions.
- **Server out of memory** — If the server is low on RAM, the .NET runtime may be under pressure and the circuit can drop. Check `docker stats` for memory usage. Consider adding a swap file if running on a 1 GB RAM instance.

### Votes Not Appearing in Real Time

**Symptom:** A user casts a vote but other users on the same group page do not see the tally update until they refresh.

**Cause:** The SignalR `VoteHub` connection has been lost silently (often a WebSocket issue — see above) and the component has fallen back to no updates rather than reconnecting.

**Fix:** Ensure WebSocket is working correctly (check nginx config). The Blazor reconnect UI should surface connection problems to the user. If the reconnect UI is not appearing, verify that the custom reconnect UI component is included in the Blazor host page.

### EF Core Migration Fails on Startup

**Symptom:** The `app` container starts, logs a migration error, and exits. `docker compose logs app` shows a migration exception.

**Possible causes:**

- The database user (`POSTGRES_USER`) does not have `CREATE TABLE` privileges. The default `playforge` user created by the `postgres` container has full ownership of the `playforge` database, so this should not occur with a default setup.
- A previous deployment left the database in a partially-migrated state (migration table has an entry but the schema object was not fully created, for example due to a mid-migration crash). Connect with `psql` and inspect the `__EFMigrationsHistory` table.
- A new migration has a breaking change (rename, column type change) that conflicts with existing data. This requires manual intervention to resolve the data issue before the migration can apply.

In all cases, the resolution is to connect directly to the Postgres container and inspect the database state:

```bash
docker compose exec postgres psql -U playforge -d playforge
\dt                             -- list tables
SELECT * FROM "__EFMigrationsHistory";  -- see which migrations have applied
```
