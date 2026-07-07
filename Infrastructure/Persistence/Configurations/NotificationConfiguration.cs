using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class NotificationConfiguration : BaseEntityConfiguration<Notification>
    {
        public override void Configure(EntityTypeBuilder<Notification> builder)
        {
            base.Configure(builder);
            
            builder.ToTable("Notifications");
            
            builder.Property(x => x.Title)
                .HasMaxLength(200)
                .IsRequired();
                
            builder.Property(x => x.Message)
                .HasMaxLength(1000)
                .IsRequired();
                
            builder.Property(x => x.Type)
                .HasMaxLength(50)
                .IsRequired();
                
            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
