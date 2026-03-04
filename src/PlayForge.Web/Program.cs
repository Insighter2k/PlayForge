using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PlayForge.Application.Interfaces;
using PlayForge.Application.Services;
using PlayForge.Domain.Interfaces;
using PlayForge.Infrastructure.Persistence;
using PlayForge.Infrastructure.Persistence.Repositories;
using PlayForge.Infrastructure.Steam;
using PlayForge.Infrastructure.Steam.Options;
using PlayForge.Web.Auth;
using PlayForge.Web.Components;
using PlayForge.Web.Hubs;
using PlayForge.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// ── Options ───────────────────────────────────────────────────────────────────
builder.Services.Configure<SteamApiOptions>(
    builder.Configuration.GetSection(SteamApiOptions.Section));

// ── Steam API Client (with Polly resilience) ──────────────────────────────────
builder.Services.AddHttpClient<SteamApiClient>()
    .AddStandardResilienceHandler();
builder.Services.AddHttpClient<SteamStoreClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(6);
    // Steam store API rejects requests without a browser-like User-Agent
    client.DefaultRequestHeaders.Add(
        "User-Agent",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
});

// ── Infrastructure ────────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository,        UserRepository>();
builder.Services.AddScoped<IGroupRepository,       GroupRepository>();
builder.Services.AddScoped<IVoteSessionRepository, VoteSessionRepository>();
builder.Services.AddScoped<IGameRepository,        GameRepository>();
builder.Services.AddScoped<IPlatformConnector,     SteamPlatformConnector>();

// ── Application ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<IPlatformConnectorRegistry, PlatformConnectorRegistry>();
builder.Services.AddScoped<ILibraryService,            LibraryService>();
builder.Services.AddScoped<IFriendService,             FriendService>();
builder.Services.AddScoped<IGroupService,              GroupService>();
builder.Services.AddScoped<IVoteService,               VoteService>();

// ── Caching ───────────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

// ── Vote real-time notifier ───────────────────────────────────────────────────
builder.Services.AddSingleton<VoteNotifier>();

// ── Auth: Steam OpenID 2.0 + Cookie ──────────────────────────────────────────
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath         = "/auth/login";
        options.LogoutPath        = "/auth/logout";
        options.ExpireTimeSpan    = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    })
    .AddSteam(options =>
    {
        options.ApplicationKey = builder.Configuration["Steam:ApiKey"] ?? string.Empty;
        options.CallbackPath   = "/signin-steam";
        options.CorrelationCookie.SameSite    = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
    });

builder.Services.AddScoped<IClaimsTransformation, SteamAuthHandler>();
builder.Services.AddAuthorization();

// ── API Controllers (game-preview hover card endpoint) ────────────────────────
builder.Services.AddControllers();

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// ── Blazor Server + SignalR ───────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();

var app = builder.Build();

// ── Auto-migrate on startup ───────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// ── Middleware ────────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
                     | ForwardedHeaders.XForwardedProto
                     | ForwardedHeaders.XForwardedHost
};
// Trust any proxy — safe because nginx is the only ingress in Docker Compose
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ── Auth challenge endpoints ──────────────────────────────────────────────────
app.MapGet("/auth/login", async (HttpContext ctx, string? returnUrl) =>
{
    var props = new AuthenticationProperties { RedirectUri = returnUrl ?? "/" };
    await ctx.ChallengeAsync("Steam", props);
});

app.MapGet("/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

// ── Blazor + SignalR ──────────────────────────────────────────────────────────
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.MapHub<VoteHub>("/hubs/vote");
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
