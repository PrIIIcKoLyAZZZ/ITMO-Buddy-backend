using ITMO.Buddy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMO.Buddy.Infrastructure.Persistence.Configurations;

public sealed class UserProfileConfiguration
    : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(
        EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles", table =>
        {
            table.HasCheckConstraint(
                "ck_user_profiles_course",
                "course IS NULL OR course > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.FirstName)
            .HasColumnName("first_name");

        builder.Property(x => x.LastName)
            .HasColumnName("last_name");

        builder.Property(x => x.PhotoObjectKey)
            .HasColumnName("photo_object_key");

        builder.Property(x => x.FacultyId)
            .HasColumnName("faculty_id");

        builder.Property(x => x.Course)
            .HasColumnName("course");

        builder.Property(x => x.AboutMe)
            .HasColumnName("about_me")
            .HasMaxLength(UserProfile.MaxAboutMeLength);

        builder.Property(x => x.Contact)
            .HasColumnName("contact");

        builder.Property(x => x.IsVisible)
            .HasColumnName("is_visible")
            .IsRequired();

        // Computed domain property.
        builder.Ignore(x => x.IsComplete);

        // Goals are persisted through ProfileGoalRow.
        builder.Ignore(x => x.Goals);

        ConfigureRelationships(builder);
        ConfigureInterests(builder);
        ConfigureSkills(builder);

        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasDatabaseName(
                "ux_user_profiles_user_id");

        builder.HasIndex(x => new
            {
                x.IsVisible,
                x.FacultyId,
                x.Course
            })
            .HasDatabaseName(
                "ix_user_profiles_visibility_faculty_course");
    }

    private static void ConfigureRelationships(
        EntityTypeBuilder<UserProfile> builder)
    {
        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<UserProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "fk_user_profiles_users_user_id");

        builder.HasOne<Faculty>()
            .WithMany()
            .HasForeignKey(x => x.FacultyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_user_profiles_faculties_faculty_id");
    }

    private static void ConfigureInterests(
        EntityTypeBuilder<UserProfile> builder)
    {
        builder.HasMany(x => x.Interests)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "ProfileInterest",

                right => right
                    .HasOne<Interest>()
                    .WithMany()
                    .HasForeignKey("interest_id")
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName(
                        "fk_profile_interests_interests"),

                left => left
                    .HasOne<UserProfile>()
                    .WithMany()
                    .HasForeignKey("profile_id")
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName(
                        "fk_profile_interests_profiles"),

                join =>
                {
                    join.ToTable("profile_interests");

                    join.IndexerProperty<Guid>("profile_id");
                    join.IndexerProperty<Guid>("interest_id");

                    join.HasKey(
                        "profile_id",
                        "interest_id");

                    join.HasIndex("interest_id")
                        .HasDatabaseName(
                            "ix_profile_interests_interest_id");
                });

        builder.Navigation(x => x.Interests)
            .HasField("_interests")
            .UsePropertyAccessMode(
                PropertyAccessMode.Field);
    }

    private static void ConfigureSkills(
        EntityTypeBuilder<UserProfile> builder)
    {
        builder.HasMany(x => x.Skills)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "ProfileSkill",

                right => right
                    .HasOne<Skill>()
                    .WithMany()
                    .HasForeignKey("skill_id")
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName(
                        "fk_profile_skills_skills"),

                left => left
                    .HasOne<UserProfile>()
                    .WithMany()
                    .HasForeignKey("profile_id")
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName(
                        "fk_profile_skills_profiles"),

                join =>
                {
                    join.ToTable("profile_skills");

                    join.IndexerProperty<Guid>("profile_id");
                    join.IndexerProperty<Guid>("skill_id");

                    join.HasKey(
                        "profile_id",
                        "skill_id");

                    join.HasIndex("skill_id")
                        .HasDatabaseName(
                            "ix_profile_skills_skill_id");
                });

        builder.Navigation(x => x.Skills)
            .HasField("_skills")
            .UsePropertyAccessMode(
                PropertyAccessMode.Field);
    }
}