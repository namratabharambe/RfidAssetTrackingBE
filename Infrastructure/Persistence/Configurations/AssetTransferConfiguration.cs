using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class AssetTransferConfiguration : BaseEntityConfiguration<AssetTransfer>
    {
        public override void Configure(EntityTypeBuilder<AssetTransfer> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("AssetTransfers");
            
            builder.Property(x => x.Status)
                .HasConversion<int>();
                
            builder.Property(x => x.Remarks)
                .HasMaxLength(500);
                
            builder.HasOne(x => x.Asset)
                .WithMany(x => x.AssetTransfers)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Restrict);
                
            builder.HasOne(x => x.SourceSite)
                .WithMany()
                .HasForeignKey(x => x.SourceSiteId)
                .OnDelete(DeleteBehavior.Restrict);
                
            builder.HasOne(x => x.DestinationSite)
                .WithMany()
                .HasForeignKey(x => x.DestinationSiteId)
                .OnDelete(DeleteBehavior.Restrict);
                
            builder.HasOne(x => x.RequestedByUser)
                .WithMany()
                .HasForeignKey(x => x.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
                
            builder.HasOne(x => x.ApprovedByUser)
                .WithMany()
                .HasForeignKey(x => x.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
