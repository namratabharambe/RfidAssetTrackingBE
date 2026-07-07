using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class ManufacturerConfiguration : BaseEntityConfiguration<Manufacturer>
    {
        public override void Configure(EntityTypeBuilder<Manufacturer> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("Manufacturers");
            
            builder.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();
                
            builder.HasIndex(x => x.Name)
                .IsUnique();
                
            builder.Property(x => x.ContactInfo)
                .HasMaxLength(500);
                
            builder.Property(x => x.SupportEmail)
                .HasMaxLength(150);
                
            builder.Property(x => x.SupportPhone)
                .HasMaxLength(50);
        }
    }
}
