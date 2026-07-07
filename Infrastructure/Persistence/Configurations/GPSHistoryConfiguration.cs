using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class GPSHistoryConfiguration : BaseEntityConfiguration<GPSHistory>
    {
        public override void Configure(EntityTypeBuilder<GPSHistory> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("GPSHistory");
            
            builder.HasOne(x => x.GPSDevice)
                .WithMany(x => x.GPSHistories)
                .HasForeignKey(x => x.GPSDeviceId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.Property(x => x.GeofenceStatus)
                .HasMaxLength(50);
                
            builder.HasIndex(x => new { x.GPSDeviceId, x.Timestamp });
        }
    }
}
