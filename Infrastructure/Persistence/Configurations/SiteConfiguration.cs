using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public class SiteConfiguration
      : BaseEntityConfiguration<Site>
    {
        public override void Configure(EntityTypeBuilder<Site> builder)
        {
            base.Configure(builder);

            builder.ToTable("Sites");

            builder.Property(x => x.Code)
                .HasMaxLength(50)
                .IsRequired();

            builder.HasIndex(x => x.Code)
                .IsUnique();

            builder.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.Address)
                .HasMaxLength(500);
        }
    }
}
