using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public class DriverConfiguration
      : BaseEntityConfiguration<Driver>
    {
        public override void Configure(EntityTypeBuilder<Driver> builder)
        {
            base.Configure(builder);

            builder.ToTable("Drivers");

            builder.Property(x => x.FullName)
                .HasMaxLength(150)
                .IsRequired();

            builder.Property(x => x.PhoneNumber)
                .HasMaxLength(30);

            builder.Property(x => x.Email)
                .HasMaxLength(100);
        }
    }
}
