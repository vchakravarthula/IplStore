using IplStore.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IplStore.Infrastructure.Data.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.EventType).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.Error).HasMaxLength(1000);

        // The processor only ever reads the small "still to do" slice of this table.
        builder.HasIndex(m => m.CreatedAtUtc)
               .HasFilter("[ProcessedAtUtc] IS NULL AND [DeadLetteredAtUtc] IS NULL");
    }
}
