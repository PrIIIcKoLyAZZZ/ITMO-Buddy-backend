using ITMO.Buddy.Domain.Listings;
using ITMO.Buddy.Domain.Users;
using ITMO.Buddy.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace ITMO.Buddy.Infrastructure.Persistence.Configurations;

internal static class SearchIndexes
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        ConfigureProfiles(modelBuilder);
        ConfigureListings(modelBuilder);
        ConfigureReferences(modelBuilder);
        ConfigureGoals(modelBuilder);
    }

    private static void ConfigureProfiles(
        ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<UserProfile>();

        builder.Property<NpgsqlTsVector>("SearchVector")
            .HasColumnName("search_vector")
            .HasColumnType("tsvector")
            .HasComputedColumnSql(
                "to_tsvector('russian'::regconfig, " +
                "coalesce(first_name, '') || ' ' || " +
                "coalesce(last_name, '') || ' ' || " +
                "coalesce(about_me, ''))",
                stored: true);

        builder.HasIndex("SearchVector")
            .HasMethod("gin")
            .HasDatabaseName(
                "ix_user_profiles_search_vector");

        builder.HasIndex(x => x.FirstName)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName(
                "ix_user_profiles_first_name_trgm");

        builder.HasIndex(x => x.LastName)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName(
                "ix_user_profiles_last_name_trgm");

        builder.HasIndex(x => x.AboutMe)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName(
                "ix_user_profiles_about_me_trgm");
    }

    private static void ConfigureListings(
        ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<Listing>();

        builder.Property<NpgsqlTsVector>("SearchVector")
            .HasColumnName("search_vector")
            .HasColumnType("tsvector")
            .HasComputedColumnSql(
                "to_tsvector('russian'::regconfig, " +
                "coalesce(title, '') || ' ' || " +
                "coalesce(description, ''))",
                stored: true);

        builder.HasIndex("SearchVector")
            .HasMethod("gin")
            .HasDatabaseName(
                "ix_listings_search_vector");

        builder.HasIndex(x => x.Title)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName(
                "ix_listings_title_trgm");

        builder.HasIndex(x => x.Description)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName(
                "ix_listings_description_trgm");
    }

    private static void ConfigureReferences(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Interest>()
            .HasIndex(x => x.Name)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName(
                "ix_interests_name_trgm");

        modelBuilder.Entity<Skill>()
            .HasIndex(x => x.Name)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName(
                "ix_skills_name_trgm");
    }

    private static void ConfigureGoals(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProfileGoalRow>()
            .HasIndex(x => new
            {
                x.GoalCategory,
                x.ProfileId
            })
            .HasDatabaseName(
                "ix_profile_goals_category_profile");
    }
}