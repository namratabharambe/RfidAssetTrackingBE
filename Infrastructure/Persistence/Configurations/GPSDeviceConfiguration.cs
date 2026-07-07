using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class GPSDeviceConfiguration : BaseEntityConfiguration<GPSDevice>
    {
        public override void Configure(EntityTypeBuilder<GPSDevice> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("GPSDevices");
            
            builder.Property(x => x.Imei)
                .HasMaxLength(50)
                .IsRequired();
                
            builder.HasIndex(x => x.Imei)
                .IsUnique();
                
            builder.Property(x => x.SimNumber)
                .HasMaxLength(50);
                
            builder.Property(x => x.Status)
                .HasConversion<int>();
                
            builder.HasOne(x => x.Asset)
                .WithMany(x => x.GPSDevices)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
