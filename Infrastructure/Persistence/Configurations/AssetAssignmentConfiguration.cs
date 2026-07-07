using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class AssetAssignmentConfiguration : BaseEntityConfiguration<AssetAssignment>
    {
        public override void Configure(EntityTypeBuilder<AssetAssignment> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("AssetAssignments");
            
            builder.Property(x => x.CustodianName)
                .HasMaxLength(200);
                
            builder.Property(x => x.Purpose)
                .HasMaxLength(500);
                
            builder.Property(x => x.Status)
                .HasMaxLength(50)
                .IsRequired();
                
            builder.Property(x => x.Notes)
                .HasMaxLength(1000);
                
            builder.HasOne(x => x.Asset)
                .WithMany(x => x.AssetAssignments)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Restrict);
                
            builder.HasOne(x => x.AssignedToUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
