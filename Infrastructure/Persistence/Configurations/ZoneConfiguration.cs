using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class ZoneConfiguration : BaseEntityConfiguration<Zone>
    {
        public override void Configure(EntityTypeBuilder<Zone> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("Zones");
            
            builder.Property(x => x.Code)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.HasIndex(x => x.Code)
                .IsUnique();
                
            builder.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();
                
            builder.Property(x => x.Description)
                .HasMaxLength(500);
                
            builder.HasOne(x => x.Warehouse)
                .WithMany(x => x.Zones)
                .HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
