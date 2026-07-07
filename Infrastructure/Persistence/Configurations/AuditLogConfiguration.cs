using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public class AuditLogConfiguration
       : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("AuditLogs");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.UserName)
                .HasMaxLength(100);

            builder.Property(x => x.Action)
                .HasMaxLength(50);

            builder.Property(x => x.EntityName)
                .HasMaxLength(100);

            builder.Property(x => x.OldValues)
                .HasColumnType("jsonb");

            builder.Property(x => x.NewValues)
                .HasColumnType("jsonb");
        }
    }
}
