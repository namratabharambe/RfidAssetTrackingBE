using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class RolePermissionConfiguration : BaseEntityConfiguration<RolePermission>
    {
        public override void Configure(EntityTypeBuilder<RolePermission> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("RolePermissions");
            
            builder.HasOne(x => x.Role)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.HasOne(x => x.Permission)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.RoleId, x.PermissionId })
                .IsUnique();
        }
    }
}
