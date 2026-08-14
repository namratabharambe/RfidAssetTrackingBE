using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public class AssetConfiguration
      : BaseEntityConfiguration<Asset>
    {
        public override void Configure(EntityTypeBuilder<Asset> builder)
        {
            base.Configure(builder);

            builder.ToTable("Assets");

            builder.Property(x => x.AssetNumber)
                .HasMaxLength(100)
                .IsRequired();

            builder.HasIndex(x => x.AssetNumber)
                .IsUnique();

            builder.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(1000);

            builder.Property(x => x.SerialNumber)
                .HasMaxLength(100);

            builder.Property(x => x.Status)
                .HasConversion<int>();

            builder.Property(x => x.DeliveryChallanNo)
                .HasMaxLength(100);

            builder.Property(x => x.InvoiceNumber)
                .HasMaxLength(100);

            builder.Property(x => x.InvoiceDate);

            builder.Property(x => x.PoNumber)
                .HasMaxLength(100);

            builder.Property(x => x.Image);

            builder.HasOne(x => x.AssetCategory)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.AssetCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
