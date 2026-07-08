using Domain.Entities;
using Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
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

        [AllowAnonymous]
        [HttpGet("equipmentnumberbyrfid/{rfidTag}")]
        public async Task<ActionResult> GetEquipmentByRfid(string rfidTag)
        {
            var tag = await _db.RFIDTags.FirstOrDefaultAsync(t => t.EpcCode == rfidTag);
            if (tag == null || tag.AssetId == null)
            {
                return NotFound($"No asset linked to RFID tag: {rfidTag}");
            }

            var asset = await _db.Assets.FindAsync(tag.AssetId.Value);
            if (asset == null)
            {
                return NotFound($"Asset not found for RFID tag: {rfidTag}");
            }

            return Ok(new
            {
                equipmentName = asset.Name,
                assetNumber = asset.AssetNumber,
                rfidTag = rfidTag
            });
        }

        [AllowAnonymous]
        [HttpPut("scanInventory")]
        public async Task<ActionResult> ScanInventory([FromBody] ScanInventoryRequestDto request)
        {
            if (request == null || request.rows == null)
            {
                return BadRequest("Invalid inventory request");
            }

            var handheld = await _db.HandheldDevices.FirstOrDefaultAsync();
            Guid? handheldGuid = handheld?.Id;

            var session = await _db.ScanSessions.FirstOrDefaultAsync(s => 
                s.IsRunning && 
                s.HandheldDeviceId == handheldGuid);

            if (session == null)
            {
                session = new ScanSession
                {
                    Id = Guid.NewGuid(),
                    SessionName = $"Handheld Inventory {DateTime.UtcNow:yyyyMMdd}",
                    StartTime = DateTime.UtcNow,
                    HandheldDeviceId = handheldGuid,
                    IsRunning = true,
                    CreatedOn = DateTime.UtcNow
                };
                _db.ScanSessions.Add(session);
            }

            foreach (var row in request.rows)
            {
                if (string.IsNullOrWhiteSpace(row.rfid)) continue;

                var scan = new RfidScan
                {
                    ScanId = Guid.NewGuid(),
                    Epc = row.rfid,
                    Rssi = -50,
                    ReaderId = handheld?.Name ?? "Handheld_C72",
                    SiteId = request.siteId.ToString(),
                    Timestamp = DateTime.UtcNow,
                    type = "Handheld",
                    CreatedOn = DateTime.UtcNow
                };
                _db.RfidScans.Add(scan);

                var scanEvent = new ScanEvent
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = session.Id,
                    EpcCode = row.rfid,
                    Timestamp = DateTime.UtcNow,
                    Rssi = -50,
                    AntennaIndex = 1,
                    HandheldDeviceId = handheldGuid,
                    Status = Domain.Enums.ScanStatus.Matched,
                    CreatedOn = DateTime.UtcNow
                };
                _db.ScanEvents.Add(scanEvent);
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [AllowAnonymous]
        [HttpPost("/api/Equipment/import-csv")]
        public async Task<ActionResult> ImportEquipment([FromBody] EquipmentImportDto request)
        {
            if (request == null || request.rows == null)
            {
                return BadRequest("Invalid import payload");
            }

            Guid siteGuid = Guid.Empty;
            if (!string.IsNullOrEmpty(request.siteId))
            {
                Guid.TryParse(request.siteId, out siteGuid);
            }

            foreach (var row in request.rows)
            {
                var categoryName = row.EquipmentType ?? "Tools";
                var category = await _db.AssetCategories.FirstOrDefaultAsync(c => c.Name.ToLower() == categoryName.ToLower());
                if (category == null)
                {
                    category = new AssetCategory
                    {
                        Id = Guid.NewGuid(),
                        Name = categoryName,
                        Description = $"{categoryName} category created from Handheld C72",
                        CreatedOn = DateTime.UtcNow
                    };
                    _db.AssetCategories.Add(category);
                }

                Guid? locationId = null;
                if (siteGuid != Guid.Empty)
                {
                    var location = await _db.Locations.FirstOrDefaultAsync(l => l.Name.ToLower() == "default location" && l.Zone.Warehouse.SiteId == siteGuid);
                    if (location == null)
                    {
                        location = await _db.Locations.FirstOrDefaultAsync(l => l.Zone.Warehouse.SiteId == siteGuid);
                    }
                    locationId = location?.Id;
                }

                var asset = await _db.Assets.FirstOrDefaultAsync(a => a.AssetNumber == row.AssetNumber);
                var statusStr = row.status?.ToLower() ?? "";
                var assetStatus = Domain.Enums.AssetStatus.Available;
                if (statusStr.Contains("maintenance")) assetStatus = Domain.Enums.AssetStatus.UnderMaintenance;
                else if (statusStr.Contains("retired")) assetStatus = Domain.Enums.AssetStatus.Retired;
                else if (statusStr.Contains("transit")) assetStatus = Domain.Enums.AssetStatus.InTransit;
                else if (statusStr.Contains("assigned") || statusStr.Contains("use")) assetStatus = Domain.Enums.AssetStatus.Assigned;

                if (asset == null)
                {
                    asset = new Asset
                    {
                        Id = Guid.NewGuid(),
                        AssetNumber = row.AssetNumber ?? $"AST-{Guid.NewGuid().ToString().Substring(0,8).ToUpper()}",
                        Name = row.EquipmentName ?? "Handheld Asset",
                        AssetCategoryId = category.Id,
                        Status = assetStatus,
                        SiteId = siteGuid == Guid.Empty ? null : siteGuid,
                        LocationId = locationId,
                        Description = "Asset created from Handheld C72 registration",
                        AssetType = "Serialized",
                        CreatedOn = DateTime.UtcNow
                    };
                    _db.Assets.Add(asset);
                }
                else
                {
                    asset.Name = row.EquipmentName ?? asset.Name;
                    asset.Status = assetStatus;
                    _db.Assets.Update(asset);
                }

                if (!string.IsNullOrEmpty(row.RfidTag))
                {
                    var existingTag = await _db.RFIDTags.FirstOrDefaultAsync(t => t.EpcCode == row.RfidTag);
                    if (existingTag == null)
                    {
                        var rfidTag = new RFIDTag
                        {
                            Id = Guid.NewGuid(),
                            EpcCode = row.RfidTag,
                            AssetId = asset.Id,
                            Status = Domain.Enums.TagStatus.Active,
                            CreatedOn = DateTime.UtcNow
                        };
                        _db.RFIDTags.Add(rfidTag);
                    }
                    else
                    {
                        existingTag.AssetId = asset.Id;
                        _db.RFIDTags.Update(existingTag);
                    }
                }
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [AllowAnonymous]
        [HttpGet("readerlist")]
        public async Task<ActionResult> GetReaderList(Guid siteId, string scanMode)
        {
            var readers = await _db.Readers
                .Where(r => r.SiteId == siteId)
                .Select(r => new
                {
                    id = r.Id,
                    name = r.Name,
                    ipAddress = r.IpAddress,
                    port = r.Port,
                    antennaCount = r.AntennaCount,
                    powerDbm = r.PowerDbm,
                    status = r.Status
                })
                .ToListAsync();

            return Ok(readers);
        }
    }

    public class ScanInventoryRequestDto
    {
        public string location { get; set; } = null!;
        public Guid siteId { get; set; }
        public List<ScanInventoryRowDto> rows { get; set; } = new();
    }

    public class ScanInventoryRowDto
    {
        public string rfid { get; set; } = null!;
    }

    public class EquipmentImportDto
    {
        public string? siteId { get; set; }
        public List<EquipmentRowDto> rows { get; set; } = new();
    }

    public class EquipmentRowDto
    {
        public string RfidTag { get; set; } = null!;
        public string? AssetNumber { get; set; }
        public string? EquipmentName { get; set; }
        public string? EquipmentType { get; set; }
        public string? imageUrl { get; set; }
        public string? lastDateTimeOut { get; set; }
        public string? lastDateTimeIn { get; set; }
        public string? hoursOnField { get; set; }
        public string? scans { get; set; }
        public string? location { get; set; }
        public string? status { get; set; }
        public string? gpsNumber { get; set; }
        public string? barcodeNumber { get; set; }
        public string? site { get; set; }
        public string? cost { get; set; }
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
