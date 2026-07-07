using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class HandheldDeviceConfiguration : BaseEntityConfiguration<HandheldDevice>
    {
        public override void Configure(EntityTypeBuilder<HandheldDevice> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("HandheldDevices");
            
            builder.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();
                
            builder.Property(x => x.DeviceSerial)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.HasIndex(x => x.DeviceSerial)
                .IsUnique();
                
            builder.Property(x => x.Model)
                .HasMaxLength(100);
                
            builder.Property(x => x.Status)
                .HasConversion<int>();
                
            builder.HasOne(x => x.AssignedUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
