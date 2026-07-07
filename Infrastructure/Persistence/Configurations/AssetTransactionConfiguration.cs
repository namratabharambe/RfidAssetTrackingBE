using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public class AssetTransactionConfiguration
      : BaseEntityConfiguration<AssetTransaction>
    {
        public override void Configure(EntityTypeBuilder<AssetTransaction> builder)
        {
            base.Configure(builder);

            builder.ToTable("AssetTransactions");

            builder.Property(x => x.TransactionType)
                .HasConversion<int>();

            builder.Property(x => x.Remarks)
                .HasMaxLength(1000);

            builder.HasOne(x => x.Asset)
                .WithMany(x => x.AssetTransactions)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
