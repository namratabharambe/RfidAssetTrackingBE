using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class ScanEventConfiguration : BaseEntityConfiguration<ScanEvent>
    {
        public override void Configure(EntityTypeBuilder<ScanEvent> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("ScanEvents");
            
            builder.Property(x => x.EpcCode)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.Property(x => x.TidCode)
                .HasMaxLength(100);
                
            builder.Property(x => x.Status)
                .HasConversion<int>();
                
            builder.HasOne(x => x.ScanSession)
                .WithMany(x => x.ScanEvents)
                .HasForeignKey(x => x.ScanSessionId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.HasOne(x => x.Reader)
                .WithMany()
                .HasForeignKey(x => x.ReaderId)
                .OnDelete(DeleteBehavior.SetNull);
                
            builder.HasOne(x => x.HandheldDevice)
                .WithMany()
                .HasForeignKey(x => x.HandheldDeviceId)
                .OnDelete(DeleteBehavior.SetNull);
                
            builder.HasIndex(x => new { x.EpcCode, x.Timestamp });
        }
    }
}
