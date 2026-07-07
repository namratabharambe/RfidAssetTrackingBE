using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public class AlertConfiguration
      : BaseEntityConfiguration<Alert>
    {
        public override void Configure(EntityTypeBuilder<Alert> builder)
        {
            base.Configure(builder);

            builder.ToTable("Alerts");

            builder.Property(x => x.AlertType)
                .HasConversion<int>();

            builder.Property(x => x.Severity)
                .HasConversion<int>();

            builder.Property(x => x.Title)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.Message)
                .HasMaxLength(2000)
                .IsRequired();

            builder.HasOne(x => x.Asset)
                .WithMany(x => x.Alerts)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
