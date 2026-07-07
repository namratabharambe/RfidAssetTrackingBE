using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class VendorConfiguration : BaseEntityConfiguration<Vendor>
    {
        public override void Configure(EntityTypeBuilder<Vendor> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("Vendors");
            
            builder.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();
                
            builder.HasIndex(x => x.Name)
                .IsUnique();
                
            builder.Property(x => x.ContactName)
                .HasMaxLength(200);
                
            builder.Property(x => x.Email)
                .HasMaxLength(150);
                
            builder.Property(x => x.Phone)
                .HasMaxLength(50);
                
            builder.Property(x => x.Address)
                .HasMaxLength(500);
        }
    }
}
