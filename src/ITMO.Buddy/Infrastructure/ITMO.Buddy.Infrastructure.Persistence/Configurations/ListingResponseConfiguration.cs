using ITMO.Buddy.Domain.Listings;
using ITMO.Buddy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMO.Buddy.Infrastructure.Persistence.Configurations;

public sealed class ListingResponseConfiguration
    : IEntityTypeConfiguration<ListingResponse>
{
    public void Configure(
        EntityTypeBuilder<ListingResponse> builder)
    {
        builder.ToTable("listing_responses", table =>
        {
            table.HasCheckConstraint(
                "ck_listing_responses_status",
                "status IN ('Pending', 'Accepted', 'Rejected')");

            table.HasCheckConstraint(
                "ck_listing_responses_decision_date",
                "decided_at_utc IS NULL OR " +
                "decided_at_utc >= created_at_utc");

            table.HasCheckConstraint(
                "ck_listing_responses_status_decision",
                "(status = 'Pending' AND decided_at_utc IS NULL) " +
                "OR (status IN ('Accepted', 'Rejected') " +
                "AND decided_at_utc IS NOT NULL)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.ListingId)
            .HasColumnName("listing_id")
            .IsRequired();

        builder.Property(x => x.CandidateId)
            .HasColumnName("candidate_id")
            .IsRequired();

        builder.Property(x => x.Message)
            .HasColumnName("message")
            .HasMaxLength(ListingResponse.MaxMessageLength);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.DecidedAtUtc)
            .HasColumnName("decided_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Ignore(x => x.IsPending);

        // Listing relationship is configured
        // in ListingConfiguration.

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CandidateId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_listing_responses_users_candidate_id");

        builder.HasIndex(x => new
            {
                x.ListingId,
                x.CandidateId
            })
            .IsUnique()
            .HasDatabaseName(
                "ux_listing_responses_listing_candidate");

        builder.HasIndex(x => new
            {
                x.ListingId,
                x.Status
            })
            .HasDatabaseName(
                "ix_listing_responses_listing_status");

        builder.HasIndex(x => new
            {
                x.CandidateId,
                x.CreatedAtUtc
            })
            .HasDatabaseName(
                "ix_listing_responses_candidate_created_at");
    }
}