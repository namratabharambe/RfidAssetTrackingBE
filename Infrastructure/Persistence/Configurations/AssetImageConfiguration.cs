using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class AssetImageConfiguration : BaseEntityConfiguration<AssetImage>
    {
        public override void Configure(EntityTypeBuilder<AssetImage> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("AssetImages");
            
            builder.Property(x => x.ImageUrl)
                .HasMaxLength(1000)
                .IsRequired();
                
            builder.HasOne(x => x.Asset)
                .WithMany(x => x.AssetImages)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
