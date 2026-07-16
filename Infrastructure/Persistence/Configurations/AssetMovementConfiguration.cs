using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class AssetMovementConfiguration : BaseEntityConfiguration<AssetMovement>
    {
        public override void Configure(EntityTypeBuilder<AssetMovement> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("AssetMovements");
            
            builder.Property(x => x.MovementType)
                .HasMaxLength(50)
                .IsRequired();
                
            builder.Property(x => x.Remarks)
                .HasMaxLength(500);
                
            builder.HasOne(x => x.Asset)
                .WithMany(x => x.AssetMovements)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Restrict);
                
            builder.HasOne(x => x.SourceLocation)
                .WithMany()
                .HasForeignKey(x => x.SourceLocationId)
                .OnDelete(DeleteBehavior.Restrict);
                
            builder.HasOne(x => x.DestinationLocation)
                .WithMany()
                .HasForeignKey(x => x.DestinationLocationId)
                .OnDelete(DeleteBehavior.Restrict);
                
            builder.HasOne(x => x.Reader)
                .WithMany()
                .HasForeignKey(x => x.ReaderId)
                .OnDelete(DeleteBehavior.SetNull);
                
            builder.HasOne(x => x.HandheldDevice)
                .WithMany()
                .HasForeignKey(x => x.HandheldDeviceId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Navigation(x => x.Asset).AutoInclude();
            builder.Navigation(x => x.SourceLocation).AutoInclude();
            builder.Navigation(x => x.DestinationLocation).AutoInclude();
            builder.Navigation(x => x.Reader).AutoInclude();
            builder.Navigation(x => x.HandheldDevice).AutoInclude();
        }
    }
}
