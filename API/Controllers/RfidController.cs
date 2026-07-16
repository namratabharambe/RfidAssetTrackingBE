using Domain.Entities;
using Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Infrastructure.Services;
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
        public async Task<ActionResult> ScanInventory(
            [FromBody] ScanInventoryRequestDto request,
            [FromServices] IHubContext<AssetTrackingHub> hubContext)
        {
            if (request == null || request.rows == null)
                return BadRequest("Invalid inventory request");

            var siteId = request.siteId;
            var operatorName = request.operatorName ?? "Handheld Operator";

            // 1. Resolve handheld device (first active one)
            var handheld = await _db.HandheldDevices
                .FirstOrDefaultAsync(h => !h.IsDeleted);
            Guid? handheldGuid = handheld?.Id;

            // 2. Resolve location: by name first, then first location in the site
            Location? resolvedLocation = null;

            // Check if location is not selected or matches "gps" (case-insensitive)
            bool isGpsLookup = string.IsNullOrWhiteSpace(request.location) || request.location.Equals("gps", StringComparison.OrdinalIgnoreCase);

            if (!isGpsLookup)
            {
                var locQuery = request.location.Trim();
                if (locQuery.Contains(" › "))
                {
                    var parts = locQuery.Split(" › ");
                    locQuery = parts[^1].Trim();
                }

                resolvedLocation = await _db.Locations
                    .Include(l => l.Zone).ThenInclude(z => z.Warehouse)
                    .FirstOrDefaultAsync(l =>
                        (l.Name.ToLower() == locQuery.ToLower() || l.Code.ToLower() == locQuery.ToLower()) &&
                        (siteId == Guid.Empty || l.Zone.Warehouse.SiteId == siteId));
            }

            // Coordinates lookup fallback (decoupled from GPSHistory/GPSDevice tables):
            double? requestLat = request.latitude;
            double? requestLon = request.longitude;

            if (resolvedLocation == null && (!requestLat.HasValue || !requestLon.HasValue))
            {
                if (!string.IsNullOrWhiteSpace(request.location))
                {
                    var gpsMatch = System.Text.RegularExpressions.Regex.Match(request.location, @"GPS\s*\(\s*([^,]+)\s*,\s*([^)]+)\s*\)");
                    if (gpsMatch.Success)
                    {
                        if (double.TryParse(gpsMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedLat) &&
                            double.TryParse(gpsMatch.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedLon))
                        {
                            requestLat = parsedLat;
                            requestLon = parsedLon;
                        }
                    }
                }
            }

            if (resolvedLocation == null && requestLat.HasValue && requestLon.HasValue)
            {
                var locations = await _db.Locations
                    .Include(l => l.Zone).ThenInclude(z => z.Warehouse)
                    .Where(l => l.Latitude != null && l.Longitude != null && (siteId == Guid.Empty || l.Zone.Warehouse.SiteId == siteId))
                    .ToListAsync();

                Location? closestLocation = null;
                double minDistance = double.MaxValue;

                foreach (var loc in locations)
                {
                    double latDiff = (double)loc.Latitude!.Value - requestLat.Value;
                    double lonDiff = (double)loc.Longitude!.Value - requestLon.Value;
                    double dist = Math.Sqrt(latDiff * latDiff + lonDiff * lonDiff);

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestLocation = loc;
                    }
                }

                if (closestLocation != null)
                {
                    resolvedLocation = closestLocation;
                }
            }

            // Final fallback to first location of the site
            if (resolvedLocation == null && siteId != Guid.Empty)
            {
                resolvedLocation = await _db.Locations
                    .Include(l => l.Zone).ThenInclude(z => z.Warehouse)
                    .FirstOrDefaultAsync(l => l.Zone.Warehouse.SiteId == siteId);
            }

            var resolvedLocationId = resolvedLocation?.Id;
            var resolvedLocationName = resolvedLocation?.Name ?? request.location;

            // 3. Create or reuse an open scan session for this handheld/site
            var session = await _db.ScanSessions.FirstOrDefaultAsync(s =>
                s.IsRunning &&
                (handheldGuid == null || s.HandheldDeviceId == handheldGuid));

            if (session == null)
            {
                var sessionName = $"{operatorName} – Inventory {DateTime.UtcNow:dd-MMM-yyyy HH:mm}";
                session = new ScanSession
                {
                    Id = Guid.NewGuid(),
                    SessionName = sessionName,
                    StartTime = DateTime.UtcNow,
                    HandheldDeviceId = handheldGuid,
                    IsRunning = true,
                    CreatedOn = DateTime.UtcNow
                };
                _db.ScanSessions.Add(session);
            }

            // 4. Process each scanned EPC
            foreach (var row in request.rows)
            {
                if (string.IsNullOrWhiteSpace(row.rfid)) continue;

                var epcClean = row.rfid.Trim();

                // Update asset location by matching RFID tag
                var tag = await _db.RFIDTags.FirstOrDefaultAsync(t =>
                    t.EpcCode.ToLower() == epcClean.ToLower() && t.AssetId != null);

                Guid? originalLocationId = null;

                if (tag != null)
                {
                    var asset = await _db.Assets.FindAsync(tag.AssetId);
                    if (asset != null)
                    {
                        originalLocationId = asset.LocationId;
                        if (resolvedLocationId.HasValue)
                            asset.LocationId = resolvedLocationId;
                        if (siteId != Guid.Empty)
                            asset.SiteId = siteId;
                        _db.Assets.Update(asset);

                        // Save asset movement
                        var movement = new AssetMovement
                        {
                            Id = Guid.NewGuid(),
                            AssetId = asset.Id,
                            SourceLocationId = originalLocationId,
                            DestinationLocationId = resolvedLocationId,
                            MovementDate = DateTime.UtcNow,
                            MovementType = "HandheldInventory",
                            HandheldDeviceId = handheldGuid,
                            Remarks = $"Scanned at {resolvedLocationName} via Handheld Inventory."
                        };
                        _db.AssetMovements.Add(movement);

                        // Reconcile with active audits
                        var activeAudits = await _db.InventoryAudits
                            .Where(x => x.Status == Domain.Enums.AuditStatus.InProgress || x.Status == Domain.Enums.AuditStatus.Scheduled)
                            .ToListAsync();

                        foreach (var audit in activeAudits)
                        {
                            var item = await _db.InventoryAuditItems
                                .FirstOrDefaultAsync(x => x.InventoryAuditId == audit.Id && x.AssetId == asset.Id);

                            if (item != null)
                            {
                                if (item.Status != Domain.Enums.AuditItemStatus.Found)
                                {
                                    item.Status = Domain.Enums.AuditItemStatus.Found;
                                    item.ScannedLocationId = resolvedLocationId;
                                    item.ScannedDate = DateTime.UtcNow;
                                    item.Notes = $"Auto-detected by ScanInventory PUT endpoint via handheld {handheld?.Name}.";
                                    _db.InventoryAuditItems.Update(item);
                                }
                            }
                            else
                            {
                                var misplacedItem = new InventoryAuditItem
                                {
                                    Id = Guid.NewGuid(),
                                    InventoryAuditId = audit.Id,
                                    AssetId = asset.Id,
                                    ExpectedLocationId = originalLocationId,
                                    ScannedLocationId = resolvedLocationId,
                                    Status = Domain.Enums.AuditItemStatus.Misplaced,
                                    ScannedDate = DateTime.UtcNow,
                                    Notes = $"Misplaced asset auto-detected by ScanInventory PUT endpoint."
                                };
                                _db.InventoryAuditItems.Add(misplacedItem);
                            }

                            if (audit.Status == Domain.Enums.AuditStatus.Scheduled)
                            {
                                audit.Status = Domain.Enums.AuditStatus.InProgress;
                                _db.InventoryAudits.Update(audit);
                            }
                        }

                        // Broadcast live scan via SignalR
                        await hubContext.Clients.All.SendAsync("ReceiveLiveScan", new
                        {
                            EpcCode = epcClean,
                            AssetName = asset.Name,
                            AssetNumber = asset.AssetNumber,
                            Timestamp = DateTime.UtcNow,
                            Rssi = -50,
                            AntennaIndex = 1,
                            Location = $"Scanned at {resolvedLocationName} via Handheld Inventory."
                        });
                    }
                }

                // Save raw scan
                _db.RfidScans.Add(new RfidScan
                {
                    ScanId = Guid.NewGuid(),
                    Epc = epcClean,
                    Rssi = -50,
                    ReaderId = handheld?.Name ?? "Handheld_C72",
                    SiteId = siteId.ToString(),
                    Timestamp = DateTime.UtcNow,
                    type = "Handheld",
                    CreatedOn = DateTime.UtcNow
                });

                // Save scan event
                _db.ScanEvents.Add(new ScanEvent
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = session.Id,
                    EpcCode = epcClean,
                    Timestamp = DateTime.UtcNow,
                    Rssi = -50,
                    AntennaIndex = 1,
                    HandheldDeviceId = handheldGuid,
                    Status = Domain.Enums.ScanStatus.Processed, // Mark processed so ScanProcessor ignores it
                    CreatedOn = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
            return Ok(new
            {
                success = true,
                resolvedLocation = resolvedLocationName,
                totalScanned = request.rows.Count,
                sessionId = session.Id,
                operator_ = operatorName
            });
        }

        [AllowAnonymous]
        [HttpGet("/api/Equipment")]
        public async Task<ActionResult> GetEquipmentList()
        {
            var assets = await _db.Assets
                .Include(a => a.AssetCategory)
                .Include(a => a.Location)
                .Include(a => a.Site)
                .Where(a => !a.IsDeleted)
                .ToListAsync();

            var tags = await _db.RFIDTags
                .Where(t => !t.IsDeleted && t.AssetId != null)
                .ToListAsync();

            var result = assets.Select(a =>
            {
                var tag = tags.FirstOrDefault(t => t.AssetId == a.Id);
                return new EquipmentRowDto
                {
                    RfidTag = tag?.EpcCode ?? "—",
                    AssetNumber = a.AssetNumber,
                    EquipmentName = a.Name,
                    EquipmentType = a.AssetCategory?.Name ?? "Tools",
                    location = a.Location?.Name ?? "Pune DC",
                    status = a.Status.ToString(),
                    site = a.Site?.Name ?? "Pune DC",
                    barcodeNumber = a.QrCode ?? "—",
                    imageUrl = ""
                };
            }).ToList();

            return Ok(result);
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
            // Try to find fixed readers for this site first
            var emptyGuid = Guid.Empty;
            var readers = await _db.Readers
                .Where(r => siteId == emptyGuid || r.SiteId == siteId)
                .Select(r => new
                {
                    ReaderId = r.Id.ToString(),
                    ReaderId2 = r.Id.ToString(),
                    id = r.Id,
                    name = r.Name,
                    ipAddress = r.IpAddress,
                    port = r.Port,
                    antennaCount = r.AntennaCount,
                    powerDbm = r.PowerDbm,
                    status = r.Status
                })
                .ToListAsync<object>();

            // Fallback: if no fixed readers, return registered handheld devices
            // The Android app uses ReaderId from this list to post scans
            if (readers.Count == 0)
            {
                var handhelds = await _db.HandheldDevices
                    .Where(h => !h.IsDeleted)
                    .Select(h => new
                    {
                        ReaderId = h.DeviceSerial,
                        ReaderId2 = h.Id.ToString(),
                        id = h.Id,
                        name = h.Name,
                        ipAddress = h.DeviceSerial,
                        port = 0,
                        antennaCount = 1,
                        powerDbm = 0,
                        status = h.Status
                    })
                    .ToListAsync<object>();
                return Ok(handhelds);
            }

            return Ok(readers);
        }

        [HttpPost("/api/admin/users/save-driver")]
        [AllowAnonymous]
        public async Task<ActionResult> SaveDriver([FromBody] RfidSaveDriverRequest request)
        {
            try
            {
                var existing = await _db.Drivers
                    .FirstOrDefaultAsync(d => d.FullName.ToLower() == request.FullName.ToLower() && !d.IsDeleted);

                if (existing == null)
                {
                    var driver = new Driver
                    {
                        Id = Guid.NewGuid(),
                        FullName = request.FullName,
                        CreatedOn = DateTime.UtcNow
                    };
                    _db.Drivers.Add(driver);
                    await _db.SaveChangesAsync();
                }

                return Ok(new { success = true, message = "Driver saved successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class RfidSaveDriverRequest
    {
        public string FullName { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string SiteId { get; set; } = null!;
    }

    public class ScanInventoryRequestDto
    {
        public string location { get; set; } = null!;
        public Guid siteId { get; set; }
        public string? operatorName { get; set; }
        public double? latitude { get; set; }
        public double? longitude { get; set; }
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
