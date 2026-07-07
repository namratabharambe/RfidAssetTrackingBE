using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class UserConfiguration : BaseEntityConfiguration<User>
    {
        public override void Configure(EntityTypeBuilder<User> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("Users");
            
            builder.Property(x => x.Username)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.HasIndex(x => x.Username)
                .HasFilter("not \"IsDeleted\"")
                .IsUnique();
                
            builder.Property(x => x.Email)
                .HasMaxLength(150)
                .IsRequired();
                
            builder.HasIndex(x => x.Email)
                .HasFilter("not \"IsDeleted\"")
                .IsUnique();
                
            builder.Property(x => x.PasswordHash)
                .IsRequired();
                
            builder.Property(x => x.PasswordSalt)
                .IsRequired();
        }
    }
}
