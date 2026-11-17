using ITMO.Buddy.Domain.Listings;
using ITMO.Buddy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMO.Buddy.Infrastructure.Persistence.Configurations;

public sealed class ListingConfiguration
    : IEntityTypeConfiguration<Listing>
{
    public void Configure(
        EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("listings", table =>
        {
            table.HasCheckConstraint(
                "ck_listings_participants",
                "required_participants_count BETWEEN 1 AND 10 " +
                "AND accepted_participants_count >= 0 " +
                "AND accepted_participants_count <= required_participants_count");

            table.HasCheckConstraint(
                "ck_listings_timestamps",
                "updated_at_utc >= created_at_utc");

            table.HasCheckConstraint(
                "ck_listings_deadline",
                "deadline_utc IS NULL OR " +
                "deadline_utc > created_at_utc");

            table.HasCheckConstraint(
                "ck_listings_event_date",
                "event_at_utc IS NULL OR " +
                "event_at_utc > created_at_utc");

            table.HasCheckConstraint(
                "ck_listings_date_order",
                "deadline_utc IS NULL OR " +
                "event_at_utc IS NULL OR " +
                "deadline_utc <= event_at_utc");

            table.HasCheckConstraint(
                "ck_listings_category",
                "category IN " +
                "('Studies', 'Projects', 'Events', 'Interests')");

            table.HasCheckConstraint(
                "ck_listings_format",
                "format IN ('Offline', 'Online')");

            table.HasCheckConstraint(
                "ck_listings_status",
                "status IN ('Open', 'Closed')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.AuthorId)
            .HasColumnName("author_id")
            .IsRequired();

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(Listing.MaxTitleLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(Listing.MaxDescriptionLength)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.RequiredParticipantsCount)
            .HasColumnName("required_participants_count")
            .IsRequired();

        builder.Property(x => x.AcceptedParticipantsCount)
            .HasColumnName("accepted_participants_count")
            .IsRequired();

        builder.Property(x => x.Format)
            .HasColumnName("format")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.DeadlineUtc)
            .HasColumnName("deadline_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.EventAtUtc)
            .HasColumnName("event_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        
        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.Ignore(x => x.IsOpen);
        builder.Ignore(x => x.HasAvailablePlaces);

        ConfigureRelationships(builder);
        ConfigureRequiredSkills(builder);

        builder.HasIndex(x => x.AuthorId)
            .HasDatabaseName("ix_listings_author_id");

        builder.HasIndex(x => new
            {
                x.Status,
                x.CreatedAtUtc
            })
            .HasDatabaseName(
                "ix_listings_status_created_at");

        builder.HasIndex(x => new
            {
                x.Category,
                x.Status
            })
            .HasDatabaseName(
                "ix_listings_category_status");

        builder.HasIndex(x => new
            {
                x.Status,
                x.DeadlineUtc
            })
            .HasDatabaseName(
                "ix_listings_status_deadline");
    }

    private static void ConfigureRelationships(
        EntityTypeBuilder<Listing> builder)
    {
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_listings_users_author_id");

        builder.HasMany(x => x.Responses)
            .WithOne()
            .HasForeignKey(x => x.ListingId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "fk_listing_responses_listings");

        builder.Navigation(x => x.Responses)
            .HasField("_responses")
            .UsePropertyAccessMode(
                PropertyAccessMode.Field);
    }

    private static void ConfigureRequiredSkills(
        EntityTypeBuilder<Listing> builder)
    {
        builder.HasMany(x => x.RequiredSkills)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "ListingRequiredSkill",

                right => right
                    .HasOne<Skill>()
                    .WithMany()
                    .HasForeignKey("skill_id")
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName(
                        "fk_listing_required_skills_skills"),

                left => left
                    .HasOne<Listing>()
                    .WithMany()
                    .HasForeignKey("listing_id")
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName(
                        "fk_listing_required_skills_listings"),

                join =>
                {
                    join.ToTable("listing_required_skills");

                    join.IndexerProperty<Guid>("listing_id");
                    join.IndexerProperty<Guid>("skill_id");

                    join.HasKey(
                        "listing_id",
                        "skill_id");

                    join.HasIndex("skill_id")
                        .HasDatabaseName(
                            "ix_listing_required_skills_skill_id");
                });

        builder.Navigation(x => x.RequiredSkills)
            .HasField("_requiredSkills")
            .UsePropertyAccessMode(
                PropertyAccessMode.Field);
    }
}