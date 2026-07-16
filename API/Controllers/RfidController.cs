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

            // Resolve Reader
            if (!string.IsNullOrEmpty(batch.ReaderId))
            {
                if (Guid.TryParse(batch.ReaderId, out var parsedReaderGuid))
                {
                    if (await _db.Readers.AnyAsync(r => r.Id == parsedReaderGuid))
                    {
                        readerGuid = parsedReaderGuid;
                    }
                }
                if (readerGuid == null)
                {
                    var reader = await _db.Readers.FirstOrDefaultAsync(r => r.Name == batch.ReaderId || r.IpAddress == batch.ReaderId);
                    if (reader != null)
                    {
                        readerGuid = reader.Id;
                    }
                }
            }

            // Resolve Handheld Device
            if (!string.IsNullOrEmpty(batch.DeviceId))
            {
                if (Guid.TryParse(batch.DeviceId, out var parsedHandheldGuid))
                {
                    if (await _db.HandheldDevices.AnyAsync(h => h.Id == parsedHandheldGuid))
                    {
                        handheldGuid = parsedHandheldGuid;
                    }
                }
                if (handheldGuid == null)
                {
                    var handheld = await _db.HandheldDevices.FirstOrDefaultAsync(h => h.DeviceSerial == batch.DeviceId || h.Name == batch.DeviceId);
                    if (handheld != null)
                    {
                        handheldGuid = handheld.Id;
                    }
                }
            }

            // Fallback for older clients sending only one identifier in either ReaderId or DeviceId
            if (readerGuid == null && handheldGuid == null)
            {
                string fallbackId = batch.ReaderId ?? batch.DeviceId ?? "";
                if (!string.IsNullOrEmpty(fallbackId))
                {
                    if (Guid.TryParse(fallbackId, out var parsedGuid))
                    {
                        if (await _db.Readers.AnyAsync(r => r.Id == parsedGuid))
                        {
                            readerGuid = parsedGuid;
                        }
                        else if (await _db.HandheldDevices.AnyAsync(h => h.Id == parsedGuid))
                        {
                            handheldGuid = parsedGuid;
                        }
                    }
                    else
                    {
                        var reader = await _db.Readers.FirstOrDefaultAsync(r => r.IpAddress == fallbackId || r.Name == fallbackId);
                        if (reader != null)
                        {
                            readerGuid = reader.Id;
                        }
                        else
                        {
                            var handheld = await _db.HandheldDevices.FirstOrDefaultAsync(h => h.DeviceSerial == fallbackId || h.Name == fallbackId);
                            if (handheld != null)
                            {
                                handheldGuid = handheld.Id;
                            }
                        }
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

            string resolvedReaderId = batch.ReaderId ?? batch.DeviceId ?? "";

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
                    Timestamp = e.Timestamp == default ? DateTime.UtcNow : e.Timestamp.ToUniversalTime(),
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
                        Timestamp = e.Timestamp == default ? DateTime.UtcNow : e.Timestamp.ToUniversalTime(),
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
