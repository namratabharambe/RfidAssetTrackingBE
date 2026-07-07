using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public class AssetCategoryConfiguration
      : BaseEntityConfiguration<AssetCategory>
    {
        public override void Configure(EntityTypeBuilder<AssetCategory> builder)
        {
            base.Configure(builder);

            builder.ToTable("AssetCategories");

            builder.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(500);

            builder.HasIndex(x => x.Name)
                .IsUnique();
        }
    }
}
