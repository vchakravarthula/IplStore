using IplStore.Domain.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Infrastructure.Data.Configurations
{
    public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            builder.HasKey(m => m.Id);
            builder.Property(m => m.EventType).HasMaxLength(200).IsRequired();
            builder.Property(m => m.Payload).IsRequired();
            builder.HasIndex(m => m.ProcessedAtUtc);

        }
    }
}
