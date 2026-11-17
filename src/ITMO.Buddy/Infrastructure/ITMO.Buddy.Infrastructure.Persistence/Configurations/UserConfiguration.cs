using ITMO.Buddy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMO.Buddy.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration
    : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", table =>
        {
            table.HasCheckConstraint(
                "ck_users_email_not_blank",
                "length(btrim(email::text)) > 0");

            table.HasCheckConstraint(
                "ck_users_password_hash_not_blank",
                "length(btrim(password_hash)) > 0");

            table.HasCheckConstraint(
                "ck_users_consent_date",
                "personal_data_consent_at_utc <= created_at_utc");

            table.HasCheckConstraint(
                "ck_users_activity_date",
                "last_activity_at_utc >= created_at_utc");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasColumnType("citext")
            .IsRequired();

        builder.Property(x => x.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.LastActivityAtUtc)
            .HasColumnName("last_activity_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.PersonalDataConsentAtUtc)
            .HasColumnName("personal_data_consent_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(x => x.Email)
            .IsUnique()
            .HasDatabaseName("ux_users_email");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_users_status");

        builder.Ignore(x => x.IsActive);
    }
}