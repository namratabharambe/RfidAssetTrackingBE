using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class AssetIssuanceConfiguration : BaseEntityConfiguration<AssetIssuance>
    {
        public override void Configure(EntityTypeBuilder<AssetIssuance> builder)
        {
            base.Configure(builder);

            builder.ToTable("AssetIssuances");
            builder.Ignore(x => x.RowVersion);
        }
    }
}
