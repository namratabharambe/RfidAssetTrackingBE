using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Context
{
    public class AssetTrackingDbContextFactory
      : IDesignTimeDbContextFactory<AssetTrackingDbContext>
    {
        public AssetTrackingDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder =
                new DbContextOptionsBuilder<AssetTrackingDbContext>();

            optionsBuilder.UseNpgsql(
                   "Host=localhost;Port=5432;Database=AssetTrackingDb;Username=postgres;Password=postgres");

            return new AssetTrackingDbContext(optionsBuilder.Options);
        }
    }
}
