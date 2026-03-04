using Microsoft.EntityFrameworkCore;
using PlayForge.Domain.Entities;
using PlayForge.Infrastructure.Persistence.Configurations;

namespace PlayForge.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User>            Users            => Set<User>();
    public DbSet<Game>            Games            => Set<Game>();
    public DbSet<UserGame>        UserGames        => Set<UserGame>();
    public DbSet<GameGroup>       GameGroups       => Set<GameGroup>();
    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
    public DbSet<VoteSession>     VoteSessions     => Set<VoteSession>();
    public DbSet<Vote>            Votes            => Set<Vote>();
    public DbSet<GroupCandidate>  GroupCandidates  => Set<GroupCandidate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new GameConfiguration());
        modelBuilder.ApplyConfiguration(new UserGameConfiguration());
        modelBuilder.ApplyConfiguration(new GameGroupConfiguration());
        modelBuilder.ApplyConfiguration(new GroupMembershipConfiguration());
        modelBuilder.ApplyConfiguration(new VoteSessionConfiguration());
        modelBuilder.ApplyConfiguration(new VoteConfiguration());
        modelBuilder.ApplyConfiguration(new GroupCandidateConfiguration());
    }
}
