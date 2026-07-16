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

        public async Task<DashboardDto> GetDashboardDataAsync(CancellationToken cancellationToken = default)
        {
            var assets = await _context.Assets.Where(x => !x.IsDeleted).ToListAsync(cancellationToken);
            
            var total = assets.Count;
            var available = assets.Count(x => x.Status == AssetStatus.Available);
            var assigned = assets.Count(x => x.Status == AssetStatus.Assigned);
            var underMaintenance = assets.Count(x => x.Status == AssetStatus.UnderMaintenance);
            var retired = assets.Count(x => x.Status == AssetStatus.Retired);
            
            var sites = await _context.Sites.Where(x => !x.IsDeleted).Include(x => x.Warehouses).ToListAsync(cancellationToken);
            var siteStats = new List<SiteStatDto>();
            foreach (var site in sites)
            {
                var siteAssets = assets.Where(x => x.SiteId == site.Id).ToList();
                siteStats.Add(new SiteStatDto(
                    site.Name,
                    siteAssets.Count,
                    siteAssets.Count(x => x.Status == AssetStatus.Assigned),
                    siteAssets.Count(x => x.Status == AssetStatus.Available),
                    siteAssets.Count(x => x.Status == AssetStatus.UnderMaintenance)
                ));
            }

            var readers = await _context.Readers.Where(x => !x.IsDeleted).Include(r => r.Site).ToListAsync(cancellationToken);
            var readerStatuses = readers.Select(r => new ReaderStatusDto(
                r.Name,
                r.Site.Name,
                r.Status.ToString()
            )).ToList();

            var gpsDevices = await _context.GPSDevices.Where(x => !x.IsDeleted).Include(g => g.Asset).ToListAsync(cancellationToken);
            var gpsStatuses = gpsDevices.Select(g => new GPSDeviceStatusDto(
                g.Imei,
                g.Asset != null ? g.Asset.Name : "Unassigned",
                g.BatteryLevel,
                g.Status.ToString()
            )).ToList();

            var movements = await _context.AssetMovements.Where(x => !x.IsDeleted)
                .Include(m => m.Asset)
                .OrderByDescending(m => m.MovementDate)
                .ToListAsync(cancellationToken);
                
            var recentActivity = movements.Select(m => new ActivityLogDto(
                $"Asset '{m.Asset.Name}' ({m.Asset.AssetNumber}) was moved. Type: {m.MovementType}.",
                m.MovementDate,
                m.CreatedBy ?? "System"
            )).ToList();

            var alerts = await _context.Alerts.Where(x => !x.IsDeleted && !x.IsResolved)
                .Include(a => a.Asset)
                .OrderByDescending(a => a.CreatedOn)
                .ToListAsync(cancellationToken);

            var activeAlerts = alerts.Select(a => new AlertDto(
                a.Id,
                a.AssetId,
                a.Asset != null ? a.Asset.Name : null,
                a.AlertType.ToString(),
                a.Severity.ToString(),
                a.Title,
                a.Message,
                a.IsResolved,
                a.ResolvedDate,
                null
            )).ToList();

            var today = DateTime.UtcNow.Date;
            var scanEvents = await _context.ScanEvents.Where(x => !x.IsDeleted && x.Timestamp >= today).ToListAsync(cancellationToken);
            
            var todayScans = scanEvents
                .GroupBy(x => x.Timestamp.Hour)
                .Select(g => new ScanCountDto($"{g.Key:00}:00", g.Count()))
                .OrderBy(x => x.Label)
                .ToList();

            var weekAgo = today.AddDays(-7);
            var weeklyEvents = await _context.ScanEvents.Where(x => !x.IsDeleted && x.Timestamp >= weekAgo).ToListAsync(cancellationToken);
            
            var weeklyScans = weeklyEvents
                .GroupBy(x => x.Timestamp.DayOfWeek)
                .Select(g => new ScanCountDto(g.Key.ToString(), g.Count()))
                .ToList();

            var yearAgo = today.AddMonths(-12);
            var monthlyEvents = await _context.ScanEvents.Where(x => !x.IsDeleted && x.Timestamp >= yearAgo).ToListAsync(cancellationToken);
            
            var monthlyScans = monthlyEvents
                .GroupBy(x => x.Timestamp.Month)
                .Select(g => new ScanCountDto(System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key), g.Count()))
                .ToList();

            return new DashboardDto(
                total,
                available,
                assigned,
                underMaintenance,
                retired,
                siteStats,
                readerStatuses,
                gpsStatuses,
                recentActivity,
                activeAlerts,
                todayScans,
                weeklyScans,
                monthlyScans
            );
        }
    }
}
