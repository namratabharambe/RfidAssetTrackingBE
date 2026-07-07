using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class InventoryAuditItemConfiguration : BaseEntityConfiguration<InventoryAuditItem>
    {
        public override void Configure(EntityTypeBuilder<InventoryAuditItem> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("InventoryAuditItems");
            
            builder.Property(x => x.Status)
                .HasConversion<int>();
                
            builder.Property(x => x.Notes)
                .HasMaxLength(500);
                
            builder.HasOne(x => x.InventoryAudit)
                .WithMany(x => x.AuditItems)
                .HasForeignKey(x => x.InventoryAuditId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.HasOne(x => x.Asset)
                .WithMany()
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Restrict);
                
            builder.HasOne(x => x.ExpectedLocation)
                .WithMany()
                .HasForeignKey(x => x.ExpectedLocationId)
                .OnDelete(DeleteBehavior.Restrict);
                
            builder.HasOne(x => x.ScannedLocation)
                .WithMany()
                .HasForeignKey(x => x.ScannedLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
