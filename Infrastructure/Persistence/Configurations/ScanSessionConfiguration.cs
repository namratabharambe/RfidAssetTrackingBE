using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class ScanSessionConfiguration : BaseEntityConfiguration<ScanSession>
    {
        public override void Configure(EntityTypeBuilder<ScanSession> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("ScanSessions");
            
            builder.Property(x => x.SessionName)
                .HasMaxLength(200)
                .IsRequired();
                
            builder.HasOne(x => x.Reader)
                .WithMany(x => x.ScanSessions)
                .HasForeignKey(x => x.ReaderId)
                .OnDelete(DeleteBehavior.SetNull);
                
            builder.HasOne(x => x.HandheldDevice)
                .WithMany(x => x.ScanSessions)
                .HasForeignKey(x => x.HandheldDeviceId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
