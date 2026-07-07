using Application.Interfaces;
using Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class ReportService : IReportService
    {
        private readonly AssetTrackingDbContext _context;

        public ReportService(AssetTrackingDbContext context)
        {
            _context = context;
        }

        private byte[] ConvertToCsvBytes(List<string> headers, List<List<string>> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers));
            foreach (var row in rows)
            {
                var escapedRow = new List<string>();
                foreach (var cell in row)
                {
                    var val = cell ?? "";
                    if (val.Contains(",") || val.Contains("\"") || val.Contains("\n"))
                    {
                        val = "\"" + val.Replace("\"", "\"\"") + "\"";
                    }
                    escapedRow.Add(val);
                }
                sb.AppendLine(string.Join(",", escapedRow));
            }
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public async Task<byte[]> GenerateAssetReportAsync(string format, CancellationToken cancellationToken = default)
        {
            var assets = await _context.Assets.Where(x => !x.IsDeleted).Include(a => a.AssetCategory).ToListAsync(cancellationToken);
            var headers = new List<string> { "Asset Id", "Asset Number", "Name", "Description", "Serial Number", "Status", "Category" };
            var rows = new List<List<string>>();
            foreach (var a in assets)
            {
                rows.Add(new List<string> {
                    a.Id.ToString(),
                    a.AssetNumber,
                    a.Name,
                    a.Description ?? "",
                    a.SerialNumber ?? "",
                    a.Status.ToString(),
                    a.AssetCategory.Name
                });
            }
            return ConvertToCsvBytes(headers, rows);
        }

        public async Task<byte[]> GenerateMovementReportAsync(string format, CancellationToken cancellationToken = default)
        {
            var movements = await _context.AssetMovements.Where(x => !x.IsDeleted)
                .Include(m => m.Asset)
                .Include(m => m.SourceLocation)
                .Include(m => m.DestinationLocation)
                .ToListAsync(cancellationToken);

            var headers = new List<string> { "Movement Id", "Asset Name", "Asset Number", "Source Location", "Destination Location", "Movement Date", "Type", "Remarks" };
            var rows = new List<List<string>>();
            foreach (var m in movements)
            {
                rows.Add(new List<string> {
                    m.Id.ToString(),
                    m.Asset.Name,
                    m.Asset.AssetNumber,
                    m.SourceLocation?.Name ?? "—",
                    m.DestinationLocation?.Name ?? "—",
                    m.MovementDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    m.MovementType,
                    m.Remarks ?? ""
                });
            }
            return ConvertToCsvBytes(headers, rows);
        }

        public async Task<byte[]> GenerateInventoryReportAsync(string format, CancellationToken cancellationToken = default)
        {
            var assets = await _context.Assets.Where(x => !x.IsDeleted).Include(a => a.AssetCategory).ToListAsync(cancellationToken);
            var headers = new List<string> { "Category Name", "Total Assets", "Available", "Assigned", "Under Maintenance", "Retired" };
            var rows = new List<List<string>>();

            var groups = assets.GroupBy(x => x.AssetCategory.Name);
            foreach (var g in groups)
            {
                rows.Add(new List<string> {
                    g.Key,
                    g.Count().ToString(),
                    g.Count(x => x.Status == Domain.Enums.AssetStatus.Available).ToString(),
                    g.Count(x => x.Status == Domain.Enums.AssetStatus.Assigned).ToString(),
                    g.Count(x => x.Status == Domain.Enums.AssetStatus.UnderMaintenance).ToString(),
                    g.Count(x => x.Status == Domain.Enums.AssetStatus.Retired).ToString()
                });
            }
            return ConvertToCsvBytes(headers, rows);
        }

        public async Task<byte[]> GenerateRFIDReportAsync(string format, CancellationToken cancellationToken = default)
        {
            var tags = await _context.RFIDTags.Where(x => !x.IsDeleted).Include(t => t.Asset).ToListAsync(cancellationToken);
            var headers = new List<string> { "Tag Id", "EPC Code", "TID Code", "Status", "Asset Name", "Asset Number" };
            var rows = new List<List<string>>();
            foreach (var t in tags)
            {
                rows.Add(new List<string> {
                    t.Id.ToString(),
                    t.EpcCode,
                    t.TidCode ?? "",
                    t.Status.ToString(),
                    t.Asset?.Name ?? "—",
                    t.Asset?.AssetNumber ?? "—"
                });
            }
            return ConvertToCsvBytes(headers, rows);
        }

        public async Task<byte[]> GenerateGPSReportAsync(string format, CancellationToken cancellationToken = default)
        {
            var history = await _context.GPSHistories.Where(x => !x.IsDeleted).Include(h => h.GPSDevice).ThenInclude(d => d.Asset).ToListAsync(cancellationToken);
            var headers = new List<string> { "Ping Id", "GPS Device IMEI", "Asset Name", "Latitude", "Longitude", "Speed", "Timestamp", "Geofence Status" };
            var rows = new List<List<string>>();
            foreach (var h in history)
            {
                rows.Add(new List<string> {
                    h.Id.ToString(),
                    h.GPSDevice.Imei,
                    h.GPSDevice.Asset?.Name ?? "—",
                    h.Latitude.ToString(),
                    h.Longitude.ToString(),
                    h.Speed.ToString(),
                    h.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    h.GeofenceStatus ?? "—"
                });
            }
            return ConvertToCsvBytes(headers, rows);
        }

        public async Task<byte[]> GenerateAuditReportAsync(string format, CancellationToken cancellationToken = default)
        {
            var audits = await _context.InventoryAudits.Where(x => !x.IsDeleted).Include(a => a.AuditorUser).ToListAsync(cancellationToken);
            var headers = new List<string> { "Audit Id", "Title", "Audit Date", "Status", "Auditor Name" };
            var rows = new List<List<string>>();
            foreach (var a in audits)
            {
                rows.Add(new List<string> {
                    a.Id.ToString(),
                    a.Title,
                    a.AuditDate.ToString("yyyy-MM-dd"),
                    a.Status.ToString(),
                    a.AuditorUser.Username
                });
            }
            return ConvertToCsvBytes(headers, rows);
        }

        public async Task<byte[]> GenerateUserReportAsync(string format, CancellationToken cancellationToken = default)
        {
            var users = await _context.Users.Where(x => !x.IsDeleted).Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ToListAsync(cancellationToken);
            var headers = new List<string> { "User Id", "Username", "Email", "Active Status", "Assigned Roles" };
            var rows = new List<List<string>>();
            foreach (var u in users)
            {
                rows.Add(new List<string> {
                    u.Id.ToString(),
                    u.Username,
                    u.Email,
                    u.IsActive.ToString(),
                    string.Join(" | ", u.UserRoles.Select(ur => ur.Role.Name))
                });
            }
            return ConvertToCsvBytes(headers, rows);
        }
    }
}
