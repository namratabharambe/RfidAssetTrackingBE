using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class RefreshTokenConfiguration : BaseEntityConfiguration<RefreshToken>
    {
        public override void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("RefreshTokens");
            
            builder.Property(x => x.Token)
                .HasMaxLength(256)
                .IsRequired();
                
            builder.HasIndex(x => x.Token)
                .IsUnique();
                
            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
