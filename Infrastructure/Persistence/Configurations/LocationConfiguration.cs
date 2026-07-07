using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public class LocationConfiguration
      : BaseEntityConfiguration<Location>
    {
        public override void Configure(EntityTypeBuilder<Location> builder)
        {
            base.Configure(builder);

            builder.ToTable("Locations");

            builder.Property(x => x.Code)
                .HasMaxLength(100)
                .IsRequired();

            builder.HasIndex(x => x.Code)
                .IsUnique();

            builder.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.Latitude)
                .HasPrecision(10, 6);

            builder.Property(x => x.Longitude)
                .HasPrecision(10, 6);

            builder.HasOne(x => x.Zone)
                .WithMany(x => x.Locations)
                .HasForeignKey(x => x.ZoneId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
