using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class InventoryAuditConfiguration : BaseEntityConfiguration<InventoryAudit>
    {
        public override void Configure(EntityTypeBuilder<InventoryAudit> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("InventoryAudits");
            
            builder.Property(x => x.Title)
                .HasMaxLength(200)
                .IsRequired();
                
            builder.Property(x => x.Status)
                .HasConversion<int>();
                
            builder.HasOne(x => x.AuditorUser)
                .WithMany()
                .HasForeignKey(x => x.AuditorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
