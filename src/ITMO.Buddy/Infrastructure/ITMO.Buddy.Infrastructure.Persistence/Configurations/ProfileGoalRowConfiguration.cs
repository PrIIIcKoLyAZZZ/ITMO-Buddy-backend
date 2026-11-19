using ITMO.Buddy.Domain.Users;
using ITMO.Buddy.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMO.Buddy.Infrastructure.Persistence.Configurations;

internal sealed class ProfileGoalRowConfiguration
    : IEntityTypeConfiguration<ProfileGoalRow>
{
    public void Configure(
        EntityTypeBuilder<ProfileGoalRow> builder)
    {
        builder.ToTable("profile_goals", table =>
        {
            table.HasCheckConstraint(
                "ck_profile_goals_category",
                "goal_category IN " +
                "('Studies', 'Projects', 'Events', 'Interests')");
        });

        builder.HasKey(x => new
        {
            x.ProfileId,
            x.GoalCategory
        });

        builder.Property(x => x.ProfileId)
            .HasColumnName("profile_id")
            .ValueGeneratedNever();

        builder.Property(x => x.GoalCategory)
            .HasColumnName("goal_category")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "fk_profile_goals_profiles");
    }
}