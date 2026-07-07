using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Context;

public class AssetTrackingDbContext : DbContext
{
    public AssetTrackingDbContext(DbContextOptions<AssetTrackingDbContext> options)
        : base(options)
    {
    }
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<AssetTag> AssetTags => Set<AssetTag>();
    public DbSet<AssetTransaction> AssetTransactions => Set<AssetTransaction>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<AssetImage> AssetImages => Set<AssetImage>();
    public DbSet<RFIDTag> RFIDTags => Set<RFIDTag>();
    public DbSet<Barcode> Barcodes => Set<Barcode>();
    public DbSet<GPSDevice> GPSDevices => Set<GPSDevice>();
    public DbSet<GPSHistory> GPSHistories => Set<GPSHistory>();
    public DbSet<Reader> Readers => Set<Reader>();
    public DbSet<HandheldDevice> HandheldDevices => Set<HandheldDevice>();
    public DbSet<AssetAssignment> AssetAssignments => Set<AssetAssignment>();
    public DbSet<AssetTransfer> AssetTransfers => Set<AssetTransfer>();
    public DbSet<AssetMovement> AssetMovements => Set<AssetMovement>();
    public DbSet<InventoryAudit> InventoryAudits => Set<InventoryAudit>();
    public DbSet<InventoryAuditItem> InventoryAuditItems => Set<InventoryAuditItem>();
    public DbSet<ScanSession> ScanSessions => Set<ScanSession>();
    public DbSet<ScanEvent> ScanEvents => Set<ScanEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Settings> Settings => Set<Settings>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<RfidScan> RfidScans => Set<RfidScan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssetTrackingDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}

