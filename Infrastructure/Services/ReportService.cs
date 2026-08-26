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

        private (DateTime? StartUtc, DateTime? EndUtc) NormalizeDateRange(DateTime? startDate, DateTime? endDate)
        {
            DateTime? startUtc = null;
            DateTime? endUtc = null;

            if (startDate.HasValue)
            {
                var d = startDate.Value;
                startUtc = DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);
            }

            if (endDate.HasValue)
            {
                var d = endDate.Value;
                endUtc = DateTime.SpecifyKind(d.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            }

            return (startUtc, endUtc);
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

        public async Task<byte[]> GenerateAssetReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default)
        {
            var (startUtc, endUtc) = NormalizeDateRange(startDate, endDate);
            var query = _context.Assets.Where(x => !x.IsDeleted);
            if (startUtc.HasValue) query = query.Where(x => x.CreatedOn >= startUtc.Value);
            if (endUtc.HasValue) query = query.Where(x => x.CreatedOn <= endUtc.Value);
            if (siteId.HasValue) query = query.Where(x => x.SiteId == siteId.Value);
            if (!string.IsNullOrWhiteSpace(siteName))
            {
                var s = siteName.Trim().ToLower();
                query = query.Where(x => x.Site != null && (x.Site.Name.ToLower() == s || x.Site.Code.ToLower() == s));
            }

            var assets = await query.Include(a => a.AssetCategory).Include(a => a.Site).ToListAsync(cancellationToken);
            var headers = new List<string> { "Asset Id", "Asset Number", "Name", "Description", "Serial Number", "Status", "Category", "Site Name", "Created Date" };
            var rows = new List<List<string>>();
            foreach (var a in assets)
            {
                rows.Add(new List<string> {
                    a.Id.ToString(),
                    a.AssetNumber ?? "—",
                    a.Name,
                    a.Description ?? "",
                    a.SerialNumber ?? "",
                    a.Status.ToString(),
                    a.AssetCategory?.Name ?? "—",
                    a.Site?.Name ?? "—",
                    a.CreatedOn.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            return ConvertToCsvBytes(headers, rows);
        }

        public async Task<byte[]> GenerateMovementReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default)
        {
            var (startUtc, endUtc) = NormalizeDateRange(startDate, endDate);
            var query = _context.AssetMovements.Where(x => !x.IsDeleted);
            if (startUtc.HasValue) query = query.Where(x => x.MovementDate >= startUtc.Value || x.CreatedOn >= startUtc.Value);
            if (endUtc.HasValue) query = query.Where(x => x.MovementDate <= endUtc.Value || x.CreatedOn <= endUtc.Value);
            if (siteId.HasValue)
            {
                query = query.Where(x => (x.Asset != null && x.Asset.SiteId == siteId.Value) || (x.Reader != null && x.Reader.SiteId == siteId.Value));
            }
            if (!string.IsNullOrWhiteSpace(siteName))
            {
                var s = siteName.Trim().ToLower();
                query = query.Where(x => (x.Asset != null && x.Asset.Site != null && (x.Asset.Site.Name.ToLower() == s || x.Asset.Site.Code.ToLower() == s))
                                      || (x.Reader != null && x.Reader.Site != null && (x.Reader.Site.Name.ToLower() == s || x.Reader.Site.Code.ToLower() == s)));
            }

            var movements = await query
                .Include(m => m.Asset).ThenInclude(a => a.Site)
                .Include(m => m.SourceLocation)
                .Include(m => m.DestinationLocation)
                .ToListAsync(cancellationToken);

            var headers = new List<string> { "Movement Id", "Asset Name", "Asset Number", "Site Name", "Source Location", "Destination Location", "Movement Date", "Type", "Remarks" };
            var rows = new List<List<string>>();
            foreach (var m in movements)
            {
                rows.Add(new List<string> {
                    m.Id.ToString(),
                    m.Asset?.Name ?? "—",
                    m.Asset?.AssetNumber ?? "—",
                    m.Asset?.Site?.Name ?? "—",
                    m.SourceLocation?.Name ?? "—",
                    m.DestinationLocation?.Name ?? "—",
                    m.MovementDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    m.MovementType,
                    m.Remarks ?? ""
                });
            }
            return ConvertToCsvBytes(headers, rows);
        }

        public async Task<byte[]> GenerateInventoryReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default)
        {
            var (startUtc, endUtc) = NormalizeDateRange(startDate, endDate);
            var query = _context.Assets.Where(x => !x.IsDeleted);
            if (startUtc.HasValue) query = query.Where(x => x.CreatedOn >= startUtc.Value);
            if (endUtc.HasValue) query = query.Where(x => x.CreatedOn <= endUtc.Value);
            if (siteId.HasValue) query = query.Where(x => x.SiteId == siteId.Value);
            if (!string.IsNullOrWhiteSpace(siteName))
            {
                var s = siteName.Trim().ToLower();
                query = query.Where(x => x.Site != null && (x.Site.Name.ToLower() == s || x.Site.Code.ToLower() == s));
            }

            var assets = await query.Include(a => a.AssetCategory).ToListAsync(cancellationToken);
            var headers = new List<string> { "Category Name", "Total Assets", "Available", "Assigned", "Under Maintenance", "Retired" };
            var rows = new List<List<string>>();

            var groups = assets.GroupBy(x => x.AssetCategory?.Name ?? "Uncategorized");
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

        public async Task<byte[]> GenerateRFIDReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default)
        {
            var (startUtc, endUtc) = NormalizeDateRange(startDate, endDate);
            var query = _context.RFIDTags.Where(x => !x.IsDeleted);
            if (startUtc.HasValue) query = query.Where(x => x.CreatedOn >= startUtc.Value);
            if (endUtc.HasValue) query = query.Where(x => x.CreatedOn <= endUtc.Value);
            if (siteId.HasValue) query = query.Where(x => x.Asset != null && x.Asset.SiteId == siteId.Value);
            if (!string.IsNullOrWhiteSpace(siteName))
            {
                var s = siteName.Trim().ToLower();
                query = query.Where(x => x.Asset != null && x.Asset.Site != null && (x.Asset.Site.Name.ToLower() == s || x.Asset.Site.Code.ToLower() == s));
            }

            var tags = await query.Include(t => t.Asset).ThenInclude(a => a.Site).ToListAsync(cancellationToken);
            var headers = new List<string> { "Tag Id", "EPC Code", "TID Code", "Status", "Asset Name", "Asset Number", "Site Name", "Created Date" };
            var rows = new List<List<string>>();
            foreach (var t in tags)
            {
                rows.Add(new List<string> {
                    t.Id.ToString(),
                    t.EpcCode,
                    t.TidCode ?? "",
                    t.Status.ToString(),
                    t.Asset?.Name ?? "—",
                    t.Asset?.AssetNumber ?? "—",
                    t.Asset?.Site?.Name ?? "—",
                    t.CreatedOn.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            return ConvertToCsvBytes(headers, rows);
        }

        public async Task<byte[]> GenerateGPSReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default)
        {
            var (startUtc, endUtc) = NormalizeDateRange(startDate, endDate);
            var query = _context.GPSHistories.Where(x => !x.IsDeleted);
            if (startUtc.HasValue) query = query.Where(x => x.Timestamp >= startUtc.Value || x.CreatedOn >= startUtc.Value);
            if (endUtc.HasValue) query = query.Where(x => x.Timestamp <= endUtc.Value || x.CreatedOn <= endUtc.Value);
            if (siteId.HasValue) query = query.Where(x => x.GPSDevice != null && x.GPSDevice.Asset != null && x.GPSDevice.Asset.SiteId == siteId.Value);
            if (!string.IsNullOrWhiteSpace(siteName))
            {
                var s = siteName.Trim().ToLower();
                query = query.Where(x => x.GPSDevice != null && x.GPSDevice.Asset != null && x.GPSDevice.Asset.Site != null && (x.GPSDevice.Asset.Site.Name.ToLower() == s || x.GPSDevice.Asset.Site.Code.ToLower() == s));
            }

            var history = await query.Include(h => h.GPSDevice).ThenInclude(d => d.Asset).ThenInclude(a => a.Site).ToListAsync(cancellationToken);
            var headers = new List<string> { "Ping Id", "GPS Device IMEI", "Asset Name", "Site Name", "Latitude", "Longitude", "Speed", "Timestamp", "Geofence Status" };
            var rows = new List<List<string>>();
            foreach (var h in history)
            {
                rows.Add(new List<string> {
                    h.Id.ToString(),
                    h.GPSDevice?.Imei ?? "—",
                    h.GPSDevice?.Asset?.Name ?? "—",
                    h.GPSDevice?.Asset?.Site?.Name ?? "—",
                    h.Latitude.ToString(),
                    h.Longitude.ToString(),
                    h.Speed.ToString(),
                    h.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    h.GeofenceStatus ?? "—"
                });
            }
            return ConvertToCsvBytes(headers, rows);
        }

        public async Task<byte[]> GenerateAuditReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default)
        {
            var (startUtc, endUtc) = NormalizeDateRange(startDate, endDate);
            var query = _context.InventoryAudits.Where(x => !x.IsDeleted);
            if (startUtc.HasValue) query = query.Where(x => x.AuditDate >= startUtc.Value || x.CreatedOn >= startUtc.Value);
            if (endUtc.HasValue) query = query.Where(x => x.AuditDate <= endUtc.Value || x.CreatedOn <= endUtc.Value);

            var audits = await query.Include(a => a.AuditorUser).ToListAsync(cancellationToken);
            var headers = new List<string> { "Audit Id", "Title", "Audit Date", "Status", "Auditor Name" };
            var rows = new List<List<string>>();
            foreach (var a in audits)
            {
                rows.Add(new List<string> {
                    a.Id.ToString(),
                    a.Title,
                    a.AuditDate.ToString("yyyy-MM-dd"),
                    a.Status.ToString(),
                    a.AuditorUser?.Username ?? "—"
                });
            }
            return ConvertToCsvBytes(headers, rows);
        }

        public async Task<byte[]> GenerateUserReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default)
        {
            var (startUtc, endUtc) = NormalizeDateRange(startDate, endDate);
            var query = _context.Users.Where(x => !x.IsDeleted);
            if (startUtc.HasValue) query = query.Where(x => x.CreatedOn >= startUtc.Value);
            if (endUtc.HasValue) query = query.Where(x => x.CreatedOn <= endUtc.Value);

            var users = await query.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ToListAsync(cancellationToken);
            var headers = new List<string> { "User Id", "Username", "Email", "Active Status", "Assigned Roles", "Created Date" };
            var rows = new List<List<string>>();
            foreach (var u in users)
            {
                rows.Add(new List<string> {
                    u.Id.ToString(),
                    u.Username,
                    u.Email,
                    u.IsActive.ToString(),
                    string.Join(" | ", u.UserRoles.Select(ur => ur.Role.Name)),
                    u.CreatedOn.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            return ConvertToCsvBytes(headers, rows);
        }

        public async Task<byte[]> GenerateTransferReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default)
        {
            var (startUtc, endUtc) = NormalizeDateRange(startDate, endDate);
            var query = _context.AssetTransfers.Where(x => !x.IsDeleted);
            if (startUtc.HasValue) query = query.Where(x => x.TransferDate >= startUtc.Value || x.CreatedOn >= startUtc.Value);
            if (endUtc.HasValue) query = query.Where(x => x.TransferDate <= endUtc.Value || x.CreatedOn <= endUtc.Value);
            if (siteId.HasValue) query = query.Where(x => x.SourceSiteId == siteId.Value || x.DestinationSiteId == siteId.Value);
            if (!string.IsNullOrWhiteSpace(siteName))
            {
                var s = siteName.Trim().ToLower();
                query = query.Where(x => (x.SourceSite != null && (x.SourceSite.Name.ToLower() == s || x.SourceSite.Code.ToLower() == s))
                                      || (x.DestinationSite != null && (x.DestinationSite.Name.ToLower() == s || x.DestinationSite.Code.ToLower() == s)));
            }

            var transfers = await query
                .Include(t => t.Asset)
                .Include(t => t.SourceSite)
                .Include(t => t.DestinationSite)
                .Include(t => t.RequestedByUser)
                .Include(t => t.ApprovedByUser)
                .ToListAsync(cancellationToken);

            var headers = new List<string> { "Transfer Id", "Asset Number", "Asset Name", "Item Name", "Source Site", "Destination Site", "Quantity", "Unit", "Transfer Date", "Status", "Requested By", "Approved By", "Remarks" };
            var rows = new List<List<string>>();
            foreach (var t in transfers)
            {
                rows.Add(new List<string> {
                    t.Id.ToString(),
                    t.Asset?.AssetNumber ?? "—",
                    t.Asset?.Name ?? "—",
                    t.ItemName ?? (t.Asset?.Name ?? "—"),
                    t.SourceSite?.Name ?? "—",
                    t.DestinationSite?.Name ?? "—",
                    t.Quantity.ToString(),
                    t.Unit ?? "Pcs",
                    t.TransferDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    t.Status.ToString(),
                    t.RequestedByUser?.Username ?? "—",
                    t.ApprovedByUser?.Username ?? "—",
                    t.Remarks ?? ""
                });
            }
            return ConvertToCsvBytes(headers, rows);
        }

        public async Task<byte[]> GenerateIssuanceReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default)
        {
            var (startUtc, endUtc) = NormalizeDateRange(startDate, endDate);
            var query = _context.AssetIssuances.Where(x => !x.IsDeleted);
            if (startUtc.HasValue) query = query.Where(x => x.IssuedDate >= startUtc.Value || x.CreatedOn >= startUtc.Value);
            if (endUtc.HasValue) query = query.Where(x => x.IssuedDate <= endUtc.Value || x.CreatedOn <= endUtc.Value);
            if (siteId.HasValue) query = query.Where(x => x.SiteId == siteId.Value);
            if (!string.IsNullOrWhiteSpace(siteName))
            {
                var s = siteName.Trim().ToLower();
                query = query.Where(x => x.SiteName.ToLower() == s || (x.Site != null && (x.Site.Name.ToLower() == s || x.Site.Code.ToLower() == s)));
            }

            var issuances = await query
                .Include(i => i.IssuedByUser)
                .OrderByDescending(i => i.IssuedDate)
                .ToListAsync(cancellationToken);

            var headers = new List<string> { "Issue Code", "Asset Number", "Asset Name", "Issued To Person", "Contractor", "Issue Quantity", "Unit", "Purpose", "Site Name", "Issued Date", "Previous Balance", "New Balance", "Issued By", "Remarks" };
            var rows = new List<List<string>>();
            foreach (var i in issuances)
            {
                rows.Add(new List<string> {
                    i.IssueCode,
                    i.AssetNumber,
                    i.AssetName,
                    i.IssuedToPerson,
                    i.Contractor,
                    i.IssueQuantity.ToString(),
                    i.Unit,
                    i.Purpose,
                    i.SiteName,
                    i.IssuedDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    i.PreviousBalanceQty.ToString(),
                    i.NewBalanceQty.ToString(),
                    i.IssuedByUser?.Username ?? "—",
                    i.Remarks ?? ""
                });
            }
            return ConvertToCsvBytes(headers, rows);
        }
    }
}
