
using ITMO.Buddy.Domain.Conversations;
using ITMO.Buddy.Domain.Interactions;
using ITMO.Buddy.Domain.Listings;
using ITMO.Buddy.Domain.Notifications;
using ITMO.Buddy.Domain.Users;
using ITMO.Buddy.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Context;

public sealed class BuddyDbContext : DbContext
{
    public BuddyDbContext(
        DbContextOptions<BuddyDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<UserProfile> UserProfiles =>
        Set<UserProfile>();

    public DbSet<RefreshToken> RefreshTokens =>
        Set<RefreshToken>();

    public DbSet<Faculty> Faculties => Set<Faculty>();

    public DbSet<Interest> Interests => Set<Interest>();

    public DbSet<Skill> Skills => Set<Skill>();

    public DbSet<Listing> Listings => Set<Listing>();

    public DbSet<ListingResponse> ListingResponses =>
        Set<ListingResponse>();

    public DbSet<UserLike> UserLikes => Set<UserLike>();

    public DbSet<UserMatch> UserMatches =>
        Set<UserMatch>();

    public DbSet<UserBlock> UserBlocks =>
        Set<UserBlock>();

    public DbSet<Conversation> Conversations =>
        Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<Notification> Notifications =>
        Set<Notification>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(BuddyDbContext).Assembly);

        SearchIndexes.Configure(modelBuilder);

        modelBuilder.Entity<UserProfile>()
            .Property<uint>("xmin")
            .IsRowVersion();
    }
}