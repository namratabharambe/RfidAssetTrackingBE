using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class PermissionConfiguration : BaseEntityConfiguration<Permission>
    {
        public override void Configure(EntityTypeBuilder<Permission> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("Permissions");
            
            builder.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.Property(x => x.Code)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.HasIndex(x => x.Code)
                .IsUnique();
        }
    }
}
