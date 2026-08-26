using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class WarehouseConfiguration : BaseEntityConfiguration<Warehouse>
    {
        public override void Configure(EntityTypeBuilder<Warehouse> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("Warehouses");
            
            builder.Property(x => x.Code)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.HasIndex(x => x.Code)
                .IsUnique();
                
            builder.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();
                
            builder.Property(x => x.Address)
                .HasMaxLength(500);
        }
    }
}
