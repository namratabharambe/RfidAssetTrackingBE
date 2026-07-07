using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public class DeviceConfiguration
     : BaseEntityConfiguration<Device>
    {
        public override void Configure(EntityTypeBuilder<Device> builder)
        {
            base.Configure(builder);

            builder.ToTable("Devices");

            builder.Property(x => x.DeviceNumber)
                .HasMaxLength(100);

            builder.HasIndex(x => x.DeviceNumber)
                .IsUnique();

            builder.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.DeviceType)
                .HasConversion<int>();

            builder.Property(x => x.Status)
                .HasConversion<int>();
        }
    }
}
