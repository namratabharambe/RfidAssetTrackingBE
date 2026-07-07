using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class ReaderConfiguration : BaseEntityConfiguration<Reader>
    {
        public override void Configure(EntityTypeBuilder<Reader> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("Readers");
            
            builder.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();
                
            builder.Property(x => x.IpAddress)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.Property(x => x.Status)
                .HasConversion<int>();
                
            builder.HasOne(x => x.Site)
                .WithMany()
                .HasForeignKey(x => x.SiteId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
