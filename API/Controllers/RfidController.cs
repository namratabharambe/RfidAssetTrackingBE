using Domain.Entities;
using Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RfidController : ControllerBase
    {
        private readonly AssetTrackingDbContext _db;

        public RfidController(AssetTrackingDbContext db)
        {
            _db = db;
        }

        [AllowAnonymous]
        [HttpPost("ingest")]
        public async Task<ActionResult> Ingest([FromBody] RfidEventBatch batch)
        {
            if (batch == null || batch.Events == null)
            {
                return BadRequest("Invalid batch payload");
            }

            // 1. Resolve reader or handheld ID
            Guid? readerGuid = null;
            Guid? handheldGuid = null;

            string resolvedReaderId = batch.ReaderId ?? batch.DeviceId ?? "";

            if (Guid.TryParse(resolvedReaderId, out var parsedGuid))
            {
                var readerExists = await _db.Readers.AnyAsync(r => r.Id == parsedGuid);
                if (readerExists)
                {
                    readerGuid = parsedGuid;
                }
                else
                {
                    var handheldExists = await _db.HandheldDevices.AnyAsync(h => h.Id == parsedGuid);
                    if (handheldExists)
                    {
                        handheldGuid = parsedGuid;
                    }
                }
            }
            else
            {
                // Try looking up by serial or ip or name
                var reader = await _db.Readers.FirstOrDefaultAsync(r => r.IpAddress == resolvedReaderId || r.Name == resolvedReaderId);
                if (reader != null)
                {
                    readerGuid = reader.Id;
                }
                else
                {
                    var handheld = await _db.HandheldDevices.FirstOrDefaultAsync(h => h.DeviceSerial == resolvedReaderId || h.Name == resolvedReaderId);
                    if (handheld != null)
                    {
                        handheldGuid = handheld.Id;
                    }
                }
            }

            // 2. Fetch or create a ScanSession if we found a valid reader/handheld
            Guid? sessionId = null;
            if (readerGuid != null || handheldGuid != null)
            {
                var session = await _db.ScanSessions.FirstOrDefaultAsync(s => 
                    s.IsRunning && 
                    ((readerGuid != null && s.ReaderId == readerGuid) || (handheldGuid != null && s.HandheldDeviceId == handheldGuid)));

                if (session == null)
                {
                    session = new ScanSession
                    {
                        Id = Guid.NewGuid(),
                        SessionName = $"Ingest Session {DateTime.UtcNow:yyyyMMdd}",
                        StartTime = DateTime.UtcNow,
                        ReaderId = readerGuid,
                        HandheldDeviceId = handheldGuid,
                        IsRunning = true,
                        CreatedOn = DateTime.UtcNow
                    };
                    _db.ScanSessions.Add(session);
                }
                sessionId = session.Id;
            }

            // 3. Save RfidScan records and generate ScanEvents
            foreach (var e in batch.Events)
            {
                var scanId = e.ScanId == Guid.Empty ? Guid.NewGuid() : e.ScanId;
                
                var scan = new RfidScan
                {
                    ScanId = scanId,
                    Epc = e.Epc,
                    Rssi = e.Rssi,
                    ReaderId = resolvedReaderId,
                    SiteId = batch.SiteId,
                    Timestamp = e.Timestamp == default ? DateTime.UtcNow : e.Timestamp,
                    type = e.type,
                    CreatedOn = DateTime.UtcNow
                };
                _db.RfidScans.Add(scan);

                if (sessionId != null)
                {
                    var scanEvent = new ScanEvent
                    {
                        Id = Guid.NewGuid(),
                        ScanSessionId = sessionId.Value,
                        EpcCode = e.Epc,
                        Timestamp = e.Timestamp == default ? DateTime.UtcNow : e.Timestamp,
                        Rssi = (int)e.Rssi,
                        AntennaIndex = 1,
                        ReaderId = readerGuid,
                        HandheldDeviceId = handheldGuid,
                        Status = Domain.Enums.ScanStatus.Matched,
                        CreatedOn = DateTime.UtcNow
                    };
                    _db.ScanEvents.Add(scanEvent);
                }
            }

            await _db.SaveChangesAsync();
            return Ok(new { Count = batch.Events.Count });
        }
    }

    public class RfidEventBatch
    {
        public string? ReaderId { get; set; }
        public string? DeviceId { get; set; } // Support alternate fixed reader payloads
        public string SiteId { get; set; } = null!;
        public List<RfidEvent> Events { get; set; } = new();
    }

    public class RfidEvent
    {
        public Guid ScanId { get; set; }
        public string Epc { get; set; } = null!;
        public double Rssi { get; set; }
        public DateTime Timestamp { get; set; }
        public string type { get; set; } = null!;
    }
}
