using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class BarcodeConfiguration : BaseEntityConfiguration<Barcode>
    {
        public override void Configure(EntityTypeBuilder<Barcode> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("Barcodes");
            
            builder.Property(x => x.BarcodeValue)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.HasIndex(x => x.BarcodeValue)
                .IsUnique();
                
            builder.Property(x => x.Format)
                .HasMaxLength(50)
                .IsRequired();
                
            builder.HasOne(x => x.Asset)
                .WithMany(x => x.Barcodes)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
