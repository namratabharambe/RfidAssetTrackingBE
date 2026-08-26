using AssetTracking.Rfid.Domain.Entities;
using Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Rfid.Infrastructure.Persistence
{
    public class AppDbContext : AssetTrackingDbContext
    {
        public AppDbContext(
            DbContextOptions<AssetTrackingDbContext> options,
            Application.Interfaces.ICurrentUserService? currentUserService = null)
            : base(options, currentUserService)
        {
        }

        public new DbSet<Alert> Alerts => RfidAlerts;
    }
}
