using ITMO.Buddy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMO.Buddy.Infrastructure.Persistence.Configurations;

public sealed class SkillConfiguration
    : IEntityTypeConfiguration<Skill>
{
    public void Configure(
        EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skills", table =>
        {
            table.HasCheckConstraint(
                "ck_skills_name_not_blank",
                "length(btrim(name)) > 0");

            table.HasCheckConstraint(
                "ck_skills_normalized_name_not_blank",
                "length(btrim(normalized_name)) > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .IsRequired();

        builder.Property(x => x.NormalizedName)
            .HasColumnName("normalized_name")
            .IsRequired();

        builder.HasIndex(x => x.NormalizedName)
            .IsUnique()
            .HasDatabaseName(
                "ux_skills_normalized_name");
    }
}