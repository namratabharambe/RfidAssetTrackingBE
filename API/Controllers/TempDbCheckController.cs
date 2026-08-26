using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence.Context;
using System.Linq;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/tempcheck")]
    public class TempDbCheckController : ControllerBase
    {
        private readonly AssetTrackingDbContext _context;

        public TempDbCheckController(AssetTrackingDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var scans = await _context.RfidScans
                .OrderByDescending(r => r.CreatedOn)
                .Take(20)
                .Select(r => new
                {
                    r.ScanId,
                    r.Epc,
                    r.Rssi,
                    r.ReaderId,
                    r.SiteId,
                    r.Timestamp,
                    r.type,
                    r.CreatedOn
                })
                .ToListAsync();

            var events = await _context.ScanEvents
                .OrderByDescending(s => s.CreatedOn)
                .Take(20)
                .Select(s => new
                {
                    s.Id,
                    s.ScanSessionId,
                    s.EpcCode,
                    s.Timestamp,
                    s.Rssi,
                    s.ReaderId,
                    s.HandheldDeviceId,
                    s.CreatedOn
                })
                .ToListAsync();

            var sessions = await _context.ScanSessions
                .OrderByDescending(s => s.CreatedOn)
                .Take(10)
                .Select(s => new
                {
                    s.Id,
                    s.SessionName,
                    s.StartTime,
                    s.EndTime,
                    s.ReaderId,
                    s.HandheldDeviceId,
                    s.IsRunning,
                    s.CreatedOn
                })
                .ToListAsync();

            var devices = await _context.HandheldDevices
                .Select(d => new
                {
                    d.Id,
                    d.DeviceSerial,
                    d.Name,
                    d.Status,
                    d.CreatedOn
                })
                .ToListAsync();

            var readers = await _context.Readers
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.IpAddress,
                    r.SiteId,
                    r.CreatedOn
                })
                .ToListAsync();

            return Ok(new
            {
                ScansCount = await _context.RfidScans.CountAsync(),
                EventsCount = await _context.ScanEvents.CountAsync(),
                SessionsCount = await _context.ScanSessions.CountAsync(),
                RecentScans = scans,
                RecentEvents = events,
                RecentSessions = sessions,
                HandheldDevices = devices,
                Readers = readers
            });
        }
    }
}
