using Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public abstract class BaseEntityConfiguration<T>
        : IEntityTypeConfiguration<T>
        where T : BaseEntity
    {
        public virtual void Configure(EntityTypeBuilder<T> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.CreatedOn)
                .IsRequired();

            builder.Property(x => x.CreatedBy)
                .HasMaxLength(100);

            builder.Property(x => x.UpdatedBy)
                .HasMaxLength(100);

            builder.Property(x => x.DeletedBy)
                .HasMaxLength(100);

            builder.Property(x => x.IsDeleted)
                .HasDefaultValue(false);

            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}
