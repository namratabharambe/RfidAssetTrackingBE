using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly AssetTrackingDbContext _context;

        public DashboardService(AssetTrackingDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardDto> GetDashboardDataAsync(Guid? siteId = null, Guid? warehouseId = null, CancellationToken cancellationToken = default)
        {
            var assetsQuery = _context.Assets
                .Include(a => a.Location).ThenInclude(l => l.Zone).ThenInclude(z => z.Warehouse)
                .Include(a => a.Site)
                .Where(x => !x.IsDeleted);

            if (siteId.HasValue)
            {
                assetsQuery = assetsQuery.Where(x => x.SiteId == siteId.Value);
            }
            if (warehouseId.HasValue)
            {
                assetsQuery = assetsQuery.Where(x => x.WarehouseId == warehouseId.Value);
            }

            var assets = await assetsQuery.ToListAsync(cancellationToken);

            var total = assets.Count;
            var available = assets.Count(x => x.Status == AssetStatus.Available);
            var assigned = assets.Count(x => x.Status == AssetStatus.Assigned);
            var underMaintenance = assets.Count(x => x.Status == AssetStatus.UnderMaintenance);
            var retired = assets.Count(x => x.Status == AssetStatus.Retired);

            // ── Site Stats: breakdown per Site ──────────────────────────────────
            IQueryable<Site> sitesQuery = _context.Sites.Where(x => !x.IsDeleted);
            if (siteId.HasValue)
            {
                sitesQuery = sitesQuery.Where(x => x.Id == siteId.Value);
            }
            var sites = await sitesQuery.ToListAsync(cancellationToken);

            var today = DateTime.UtcNow.Date;
            var siteStats = new List<SiteStatDto>();
            foreach (var site in sites)
            {
                var siteAssets = assets.Where(x => x.SiteId == site.Id).ToList();
                var siteIdStr = site.Id.ToString();

                var rfidReads = await _context.RfidScans
                    .Where(s => s.SiteId == siteIdStr && s.Timestamp >= today)
                    .CountAsync(cancellationToken);

                var gpsPings = await _context.GPSHistories
                    .Include(h => h.GPSDevice)
                    .ThenInclude(d => d.Asset)
                    .Where(h => h.GPSDevice.Asset != null && h.GPSDevice.Asset.SiteId == site.Id && h.Timestamp >= today)
                    .CountAsync(cancellationToken);

                var missingOrOverdueCount = await _context.AssetAssignments
                    .Where(a => a.Status == "Active" && a.ExpectedReturnDate < DateTime.UtcNow && (a.Asset == null || a.Asset.SiteId == site.Id))
                    .CountAsync(cancellationToken);

                var dbAlertsCount = await _context.Alerts
                    .Where(a => !a.IsDeleted && !a.IsResolved && (a.Asset == null || a.Asset.SiteId == site.Id))
                    .CountAsync(cancellationToken);

                var missingAssetsCount = await _context.Assets
                    .Where(a => !a.IsDeleted && a.SiteId == site.Id && (a.Status == AssetStatus.Assigned || a.Status == AssetStatus.UnderMaintenance))
                    .CountAsync(cancellationToken);

                var alertsCount = dbAlertsCount + missingOrOverdueCount;
                if (alertsCount == 0 && siteAssets.Any())
                {
                    // Ensure active exceptions (e.g. overdue checkouts or missing tags) are reflected
                    alertsCount = Math.Max(missingOrOverdueCount, dbAlertsCount);
                }

                var complianceTasks = await _context.InventoryAudits
                    .Where(a => a.Status == AuditStatus.Scheduled && a.AuditItems.Any(i => i.Asset.SiteId == site.Id))
                    .CountAsync(cancellationToken);

                siteStats.Add(new SiteStatDto(
                    site.Name,
                    siteAssets.Count,
                    siteAssets.Count(x => x.Status == AssetStatus.Assigned),
                    siteAssets.Count(x => x.Status == AssetStatus.Available),
                    siteAssets.Count(x => x.Status == AssetStatus.UnderMaintenance),
                    rfidReads,
                    gpsPings,
                    alertsCount,
                    complianceTasks
                ));
            }

            // ── Reader Statuses ──────────────────────────────────────────────────
            IQueryable<Reader> readersQuery = _context.Readers.Where(x => !x.IsDeleted);
            if (siteId.HasValue)
            {
                readersQuery = readersQuery.Where(r => r.SiteId == siteId.Value);
            }
            var readers = await readersQuery.Include(r => r.Site).Take(10).ToListAsync(cancellationToken);

            var readerStatuses = readers.Select(r => new ReaderStatusDto(
                r.Name,
                r.Site?.Name ?? "—",
                r.Status.ToString()
            )).ToList();

            // ── GPS Device Statuses ──────────────────────────────────────────────
            IQueryable<GPSDevice> gpsDevicesQuery = _context.GPSDevices.Where(x => !x.IsDeleted);
            if (siteId.HasValue)
            {
                gpsDevicesQuery = gpsDevicesQuery.Where(g => g.Asset != null && g.Asset.SiteId == siteId.Value);
            }
            var gpsDevices = await gpsDevicesQuery.Include(g => g.Asset).Take(10).ToListAsync(cancellationToken);

            var gpsStatuses = gpsDevices.Select(g => new GPSDeviceStatusDto(
                g.Imei,
                g.Asset != null ? g.Asset.Name : "Unassigned",
                g.BatteryLevel,
                g.Status.ToString()
            )).ToList();

            // ── Recent Activity: from scan events + movements ────────────────────
            var recentScanEvents = await _context.ScanEvents
                .Where(x => !x.IsDeleted)
                .Include(e => e.HandheldDevice)
                .Include(e => e.Reader)
                .OrderByDescending(e => e.Timestamp)
                .Take(10)
                .ToListAsync(cancellationToken);

            var recentActivity = recentScanEvents.Select(e =>
            {
                var src = e.Reader?.Name ?? e.HandheldDevice?.Name ?? "Reader";
                var tag = e.EpcCode ?? "—";
                return new ActivityLogDto(
                    $"Tag [{tag}] scanned via {src}",
                    e.Timestamp,
                    e.HandheldDevice?.Name ?? e.Reader?.Name ?? "System"
                );
            }).ToList();

            // Also pull last 5 asset movements and merge
            var movements = await _context.AssetMovements
                .Where(x => !x.IsDeleted)
                .Include(m => m.Asset)
                .Include(m => m.DestinationLocation)
                .OrderByDescending(m => m.MovementDate)
                .ToListAsync(cancellationToken);

            foreach (var m in movements)
            {
                recentActivity.Add(new ActivityLogDto(
                    $"Asset '{m.Asset?.Name}' ({m.Asset?.AssetNumber}) → {m.DestinationLocation?.Name ?? "Unknown"}. [{m.MovementType}]",
                    m.MovementDate,
                    m.CreatedBy ?? "System"
                ));
            }

            recentActivity = recentActivity.OrderByDescending(a => a.Timestamp).Take(10).ToList();

            // ── Alerts ──────────────────────────────────────────────────────────
            var alerts = await _context.Alerts
                .Where(x => !x.IsDeleted && !x.IsResolved)
                .Include(a => a.Asset)
                .OrderByDescending(a => a.CreatedOn)
                .ToListAsync(cancellationToken);

            var activeAlerts = alerts.Select(a => new AlertDto
            {
                Id = a.Id,
                AssetId = a.AssetId,
                AssetName = a.Asset?.Name,
                AlertType = a.AlertType.ToString(),
                Severity = a.Severity.ToString(),
                Title = a.Title,
                Message = a.Message,
                IsResolved = a.IsResolved,
                ResolvedDate = a.ResolvedDate,
                ResolvedByUsername = null
            }).ToList();

            // ── Scan Counts (Today / Weekly / Monthly) ────────────────────────────
            today = DateTime.UtcNow.Date;
            var scanEvents = await _context.ScanEvents
                .Where(x => !x.IsDeleted && x.Timestamp >= today)
                .ToListAsync(cancellationToken);

            var todayScans = scanEvents
                .GroupBy(x => x.Timestamp.Hour)
                .Select(g => new ScanCountDto($"{g.Key:00}:00", g.Count()))
                .OrderBy(x => x.Label)
                .ToList();

            var weekAgo = today.AddDays(-7);
            var weeklyEvents = await _context.ScanEvents
                .Where(x => !x.IsDeleted && x.Timestamp >= weekAgo)
                .ToListAsync(cancellationToken);

            var weeklyScans = weeklyEvents
                .GroupBy(x => x.Timestamp.DayOfWeek)
                .Select(g => new ScanCountDto(g.Key.ToString(), g.Count()))
                .ToList();

            var yearAgo = today.AddMonths(-12);
            var monthlyEvents = await _context.ScanEvents
                .Where(x => !x.IsDeleted && x.Timestamp >= yearAgo)
                .ToListAsync(cancellationToken);

            var monthlyScans = monthlyEvents
                .GroupBy(x => x.Timestamp.Month)
                .Select(g => new ScanCountDto(
                    System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key),
                    g.Count()))
                .ToList();

            return new DashboardDto(
                total, available, assigned, underMaintenance, retired,
                siteStats, readerStatuses, gpsStatuses,
                recentActivity, activeAlerts,
                todayScans, weeklyScans, monthlyScans
            );
        }
    }
}
