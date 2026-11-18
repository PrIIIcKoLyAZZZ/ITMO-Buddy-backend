using ITMO.Buddy.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMO.Buddy.Infrastructure.Persistence.Configurations;

internal sealed class FileCleanupEntryConfiguration : IEntityTypeConfiguration<FileCleanupEntry>
{
    public void Configure(EntityTypeBuilder<FileCleanupEntry> builder)
    {
        builder.ToTable("file_cleanup_queue");
        builder.HasKey(entry => entry.ObjectKey);
        builder.Property(entry => entry.ObjectKey).HasColumnName("object_key");
        builder.Property(entry => entry.EnqueuedAtUtc)
            .HasColumnName("enqueued_at_utc")
            .HasDefaultValueSql("now()");
    }
}
