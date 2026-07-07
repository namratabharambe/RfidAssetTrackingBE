using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class RFIDTagConfiguration : BaseEntityConfiguration<RFIDTag>
    {
        public override void Configure(EntityTypeBuilder<RFIDTag> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("RFIDTags");
            
            builder.Property(x => x.EpcCode)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.HasIndex(x => x.EpcCode)
                .IsUnique();
                
            builder.Property(x => x.TidCode)
                .HasMaxLength(100);
                
            builder.Property(x => x.Status)
                .HasConversion<int>();
                
            builder.HasOne(x => x.Asset)
                .WithMany(x => x.RFIDTags)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
