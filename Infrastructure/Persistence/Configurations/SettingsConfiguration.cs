using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class SettingsConfiguration : BaseEntityConfiguration<Settings>
    {
        public override void Configure(EntityTypeBuilder<Settings> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("Settings");
            
            builder.Property(x => x.Key)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.HasIndex(x => x.Key)
                .IsUnique();
                
            builder.Property(x => x.Value)
                .HasMaxLength(1000)
                .IsRequired();
                
            builder.Property(x => x.Description)
                .HasMaxLength(500);
                
            builder.Property(x => x.Group)
                .HasMaxLength(100)
                .IsRequired();
        }
    }
}
