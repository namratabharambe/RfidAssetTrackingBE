using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class RoleConfiguration : BaseEntityConfiguration<Role>
    {
        public override void Configure(EntityTypeBuilder<Role> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("Roles");
            
            builder.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.HasIndex(x => x.Name)
                .IsUnique();
                
            builder.Property(x => x.Description)
                .HasMaxLength(500);
        }
    }
}
