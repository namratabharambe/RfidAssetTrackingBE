using Application.Interfaces;
using Domain.Common;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Persistence.Context;

public class AssetTrackingDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public AssetTrackingDbContext(
        DbContextOptions<AssetTrackingDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
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
    public DbSet<AssetIssuance> AssetIssuances => Set<AssetIssuance>();
    public DbSet<InventoryAudit> InventoryAudits => Set<InventoryAudit>();
    public DbSet<InventoryAuditItem> InventoryAuditItems => Set<InventoryAuditItem>();
    public DbSet<ScanSession> ScanSessions => Set<ScanSession>();
    public DbSet<ScanEvent> ScanEvents => Set<ScanEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Settings> Settings => Set<Settings>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<RfidScan> RfidScans => Set<RfidScan>();

    // Custom RFID Processing entities
    public DbSet<AssetTracking.Rfid.Domain.Entities.RfidTag> CustomRfidTags => Set<AssetTracking.Rfid.Domain.Entities.RfidTag>();
    public DbSet<AssetTracking.Rfid.Domain.Entities.Truck> Trucks => Set<AssetTracking.Rfid.Domain.Entities.Truck>();
    public DbSet<AssetTracking.Rfid.Domain.Entities.Equipment> Equipment => Set<AssetTracking.Rfid.Domain.Entities.Equipment>();
    public DbSet<AssetTracking.Rfid.Domain.Entities.GateEvent> GateEvents => Set<AssetTracking.Rfid.Domain.Entities.GateEvent>();
    public DbSet<AssetTracking.Rfid.Domain.Entities.GateEventItem> GateEventItems => Set<AssetTracking.Rfid.Domain.Entities.GateEventItem>();
    public DbSet<AssetTracking.Rfid.Domain.Entities.ActiveTruckSession> ActiveTruckSessions => Set<AssetTracking.Rfid.Domain.Entities.ActiveTruckSession>();
    public DbSet<AssetTracking.Rfid.Domain.Entities.TruckEquipmentAssignment> TruckEquipmentAssignments => Set<AssetTracking.Rfid.Domain.Entities.TruckEquipmentAssignment>();
    public DbSet<AssetTracking.Rfid.Domain.Entities.MissingEquipmentCase> MissingEquipmentCases => Set<AssetTracking.Rfid.Domain.Entities.MissingEquipmentCase>();
    public DbSet<AssetTracking.Rfid.Domain.Entities.MissingEquipmentCaseItem> MissingEquipmentCaseItems => Set<AssetTracking.Rfid.Domain.Entities.MissingEquipmentCaseItem>();
    public DbSet<AssetTracking.Rfid.Domain.Entities.MissingEquipmentStatus> MissingEquipmentStatuses => Set<AssetTracking.Rfid.Domain.Entities.MissingEquipmentStatus>();
    public DbSet<AssetTracking.Rfid.Domain.Entities.MissingEquipmentSeverity> MissingEquipmentSeverities => Set<AssetTracking.Rfid.Domain.Entities.MissingEquipmentSeverity>();
    public DbSet<AssetTracking.Rfid.Domain.Entities.Alert> RfidAlerts => Set<AssetTracking.Rfid.Domain.Entities.Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssetTrackingDbContext).Assembly);

        base.OnModelCreating(modelBuilder);

        // Map custom RFID entities
        modelBuilder.Entity<AssetTracking.Rfid.Domain.Entities.Alert>().ToTable("RfidAlerts");

        modelBuilder.Entity<AssetTracking.Rfid.Domain.Entities.RfidTag>().HasKey(t => t.RfidTagId);
        modelBuilder.Entity<AssetTracking.Rfid.Domain.Entities.Truck>().HasKey(t => t.TruckId);
        modelBuilder.Entity<AssetTracking.Rfid.Domain.Entities.Equipment>().HasKey(e => e.EquipmentId);
        modelBuilder.Entity<AssetTracking.Rfid.Domain.Entities.GateEvent>().HasKey(g => g.GateEventId);
        modelBuilder.Entity<AssetTracking.Rfid.Domain.Entities.GateEventItem>().HasKey(gi => gi.GateEventItemId);
        modelBuilder.Entity<AssetTracking.Rfid.Domain.Entities.ActiveTruckSession>().HasKey(a => a.Id);
        modelBuilder.Entity<AssetTracking.Rfid.Domain.Entities.TruckEquipmentAssignment>().HasKey(a => a.AssignmentId);
        modelBuilder.Entity<AssetTracking.Rfid.Domain.Entities.MissingEquipmentCase>().HasKey(c => c.MissingEquipmentCaseId);
        modelBuilder.Entity<AssetTracking.Rfid.Domain.Entities.MissingEquipmentCaseItem>().HasKey(ci => ci.MissingEquipmentCaseItemId);
        modelBuilder.Entity<AssetTracking.Rfid.Domain.Entities.MissingEquipmentStatus>().HasKey(s => s.StatusId);
        modelBuilder.Entity<AssetTracking.Rfid.Domain.Entities.MissingEquipmentSeverity>().HasKey(s => s.SeverityId);
    }

    private void ApplyAuditInformation()
    {
        var currentUserIdStr = _currentUserService?.UserId?.ToString()
                               ?? _currentUserService?.Username
                               ?? "System";
        var now = DateTime.UtcNow;

        var entries = ChangeTracker.Entries<BaseEntity>();
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedOn == default || entry.Entity.CreatedOn.Kind != DateTimeKind.Utc)
                {
                    entry.Entity.CreatedOn = now;
                }
                if (string.IsNullOrWhiteSpace(entry.Entity.CreatedBy))
                {
                    entry.Entity.CreatedBy = currentUserIdStr;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedOn = now;
                entry.Entity.UpdatedBy = currentUserIdStr;
            }
            else if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedOn = now;
                entry.Entity.DeletedBy = currentUserIdStr;
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInformation();
        return base.SaveChanges();
    }
}

