using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public class AssetTagConfiguration
     : BaseEntityConfiguration<AssetTag>
    {
        public override void Configure(EntityTypeBuilder<AssetTag> builder)
        {
            base.Configure(builder);

            builder.ToTable("AssetTags");

            builder.Property(x => x.TagNumber)
                .HasMaxLength(100)
                .IsRequired();

            builder.HasIndex(x => x.TagNumber)
                .IsUnique();

            builder.Property(x => x.TagType)
                .HasConversion<int>();

            builder.HasOne(x => x.Asset)
                .WithMany(x => x.AssetTags)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
