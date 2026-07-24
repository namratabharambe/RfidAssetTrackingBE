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
            try
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


            // 2. Fetch or create a ScanSession for handheld / reader scans
            Guid? sessionId = null;
            var session = await _db.ScanSessions.FirstOrDefaultAsync(s => 
                s.IsRunning && 
                ((readerGuid != null && s.ReaderId == readerGuid) || 
                 (handheldGuid != null && s.HandheldDeviceId == handheldGuid) ||
                 (!string.IsNullOrEmpty(batch.OperatorName) && s.SessionName == $"Operator: {batch.OperatorName}")));

            if (session == null)
            {
                string sessionName = !string.IsNullOrEmpty(batch.OperatorName) 
                    ? $"Operator: {batch.OperatorName}" 
                    : $"Ingest Session {DateTime.UtcNow:yyyyMMdd}";

                session = new ScanSession
                {
                    Id = Guid.NewGuid(),
                    SessionName = sessionName,
                    StartTime = DateTime.UtcNow,
                    ReaderId = readerGuid,
                    HandheldDeviceId = handheldGuid,
                    IsRunning = true,
                    CreatedOn = DateTime.UtcNow
                };
                _db.ScanSessions.Add(session);
            }
            else if (!string.IsNullOrEmpty(batch.OperatorName))
            {
                // Update session name if operator is provided
                session.SessionName = $"Operator: {batch.OperatorName}";
                _db.ScanSessions.Update(session);
            }
            sessionId = session.Id;

            string resolvedReaderId = batch.ReaderId ?? batch.DeviceId ?? "";

            // Resolve Reader Direction / Name for CheckIn / CheckOut determination
            string isEntryOrExit = "";
            Reader? matchedReader = null;
            if (readerGuid != null)
            {
                matchedReader = await _db.Readers.FindAsync(readerGuid);
            }
            else if (!string.IsNullOrEmpty(resolvedReaderId))
            {
                matchedReader = await _db.Readers.FirstOrDefaultAsync(r => r.Name == resolvedReaderId || r.IpAddress == resolvedReaderId || r.Id.ToString() == resolvedReaderId);
            }

            if (matchedReader != null)
            {
                var dirStr = (matchedReader.Direction ?? "").ToUpper();
                var nameStr = (matchedReader.Name ?? "").ToUpper();
                if (dirStr == "EXIT" || nameStr.Contains("EXIT")) isEntryOrExit = "EXIT";
                else if (dirStr == "ENTRY" || nameStr.Contains("ENTRY")) isEntryOrExit = "ENTRY";
            }
            
            if (string.IsNullOrEmpty(isEntryOrExit) && !string.IsNullOrEmpty(batch.ScanMode))
            {
                if (batch.ScanMode.ToUpper().Contains("EXIT")) isEntryOrExit = "EXIT";
                else if (batch.ScanMode.ToUpper().Contains("ENTRY")) isEntryOrExit = "ENTRY";
            }

            // Numeric or custom ReaderId fallback mapping ("1", "2", "3", "4", "Reader 1", etc.)
            if (string.IsNullOrEmpty(isEntryOrExit) && !string.IsNullOrEmpty(resolvedReaderId))
            {
                var rClean = resolvedReaderId.Trim().ToLower();
                if (rClean == "1" || rClean == "3" || rClean.Contains("entry") || rClean.Contains("in") || rClean.Contains("reader 1") || rClean.Contains("reader 3") || rClean.Contains("door 1"))
                {
                    isEntryOrExit = "ENTRY";
                    if (matchedReader == null)
                        matchedReader = await _db.Readers.FirstOrDefaultAsync(r => (r.Name != null && r.Name.ToLower().Contains("entry")) || (r.Direction != null && r.Direction.ToLower() == "entry"));
                }
                else if (rClean == "2" || rClean == "4" || rClean.Contains("exit") || rClean.Contains("out") || rClean.Contains("reader 2") || rClean.Contains("reader 4") || rClean.Contains("door 2"))
                {
                    isEntryOrExit = "EXIT";
                    if (matchedReader == null)
                        matchedReader = await _db.Readers.FirstOrDefaultAsync(r => (r.Name != null && r.Name.ToLower().Contains("exit")) || (r.Direction != null && r.Direction.ToLower() == "exit"));
                }
            }

            // AntennaId / AntennaPort direction fallback mapping (Antenna 1/3 = Entry, Antenna 2/4 = Exit)
            if (string.IsNullOrEmpty(isEntryOrExit))
            {
                int? antId = batch.GetParsedAntennaId();
                if (antId == null)
                {
                    var firstEvt = batch.Events.FirstOrDefault(e => e.GetParsedAntennaId().HasValue);
                    antId = firstEvt?.GetParsedAntennaId();
                }

                if (antId == 1 || antId == 3) isEntryOrExit = "ENTRY";
                else if (antId == 2 || antId == 4) isEntryOrExit = "EXIT";
            }

            var resolvedSiteId = batch.SiteId;
            if (!string.IsNullOrEmpty(resolvedSiteId))
            {
                var sMatch = await _db.Sites.FirstOrDefaultAsync(s => !s.IsDeleted && 
                    (s.Id.ToString() == resolvedSiteId || 
                     (s.Code != null && s.Code.ToLower() == resolvedSiteId.Trim().ToLower()) || 
                     (s.Name != null && s.Name.ToLower().Contains(resolvedSiteId.Trim().ToLower()))));
                if (sMatch != null)
                {
                    resolvedSiteId = sMatch.Id.ToString();
                }
            }

            if (string.IsNullOrEmpty(resolvedSiteId) || !Guid.TryParse(resolvedSiteId, out _))
            {
                var defaultSite = await _db.Sites.FirstOrDefaultAsync(s => !s.IsDeleted && s.Name.Contains("Pune"));
                defaultSite = defaultSite ?? await _db.Sites.FirstOrDefaultAsync(s => !s.IsDeleted);
                if (defaultSite != null)
                {
                    resolvedSiteId = defaultSite.Id.ToString();
                }
            }

            // 3. Save RfidScan records and generate ScanEvents / AssetMovements
            foreach (var e in batch.Events)
            {
                var scanId = (e.ScanId == Guid.Empty || await _db.RfidScans.AnyAsync(s => s.ScanId == e.ScanId)) ? Guid.NewGuid() : e.ScanId;
                var scanTs = e.Timestamp == default ? DateTime.UtcNow : e.Timestamp.ToUniversalTime();
                var eventAntennaId = e.GetParsedAntennaId() ?? batch.GetParsedAntennaId() ?? 1;
                
                var scanTypeStr = string.IsNullOrEmpty(batch.ScanMode) ? e.type : $"{e.type}|{batch.ScanMode}";
                if (!string.IsNullOrEmpty(isEntryOrExit) && !scanTypeStr.ToUpper().Contains(isEntryOrExit))
                {
                    scanTypeStr = $"{scanTypeStr}|{isEntryOrExit}";
                }
                scanTypeStr = $"{scanTypeStr}|Antenna:{eventAntennaId}";

                var scan = new RfidScan
                {
                    ScanId = scanId,
                    Epc = e.Epc,
                    Rssi = e.Rssi,
                    ReaderId = resolvedReaderId,
                    SiteId = resolvedSiteId ?? "f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c91",
                    Timestamp = scanTs,
                    type = scanTypeStr,
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
                        Timestamp = scanTs,
                        Rssi = (int)e.Rssi,
                        AntennaIndex = eventAntennaId,
                        ReaderId = readerGuid,
                        HandheldDeviceId = handheldGuid,
                        Status = Domain.Enums.ScanStatus.Matched,
                        CreatedOn = DateTime.UtcNow
                    };
                    _db.ScanEvents.Add(scanEvent);
                }

                // If scanned by Entry or Exit reader, create AssetMovement & update Asset status
                if (!string.IsNullOrEmpty(e.Epc))
                {
                    var cleanEpc = e.Epc.Trim().ToLower();
                    var tag = await _db.RFIDTags.FirstOrDefaultAsync(t => t.EpcCode.ToLower() == cleanEpc);
                    if (tag != null && tag.AssetId != null)
                    {
                        var asset = await _db.Assets.FindAsync(tag.AssetId.Value);
                        if (asset != null)
                        {
                            string movType = isEntryOrExit == "EXIT" ? "CheckOut" : (isEntryOrExit == "ENTRY" ? "CheckIn" : "FixedReaderScan");
                            var movement = new AssetMovement
                            {
                                Id = Guid.NewGuid(),
                                AssetId = asset.Id,
                                ReaderId = matchedReader?.Id ?? readerGuid,
                                MovementDate = scanTs,
                                MovementType = movType,
                                Remarks = $"Scanned at Fixed Reader ({matchedReader?.Name ?? resolvedReaderId}) Antenna #{eventAntennaId} [{movType}]."
                            };
                            _db.AssetMovements.Add(movement);

                            if (isEntryOrExit == "EXIT")
                            {
                                asset.Status = Domain.Enums.AssetStatus.Assigned;
                                _db.Assets.Update(asset);
                            }
                            else if (isEntryOrExit == "ENTRY")
                            {
                                asset.Status = Domain.Enums.AssetStatus.Available;
                                _db.Assets.Update(asset);
                            }
                        }
                    }
                }
            }

            await _db.SaveChangesAsync();
            return Ok(new { Count = batch.Events.Count });
            }
            catch (Exception)
            {
                return Ok(new { Count = batch?.Events?.Count ?? 0, status = "processed" });
            }
        }

        [AllowAnonymous]
        [HttpGet("equipmentnumberbyrfid/{rfidTag}")]
        public async Task<ActionResult> GetEquipmentByRfid(string rfidTag)
        {
            var tag = await _db.RFIDTags.FirstOrDefaultAsync(t => t.EpcCode == rfidTag);
            if (tag == null || tag.AssetId == null)
            {
                return Ok(new List<object>());
            }

            var asset = await _db.Assets.FindAsync(tag.AssetId.Value);
            if (asset == null)
            {
                return Ok(new List<object>());
            }

            return Ok(new List<object>
            {
                new
                {
                    equipmentName = asset.Name,
                    assetNumber = asset.AssetNumber,
                    rfidTag = rfidTag
                }
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

            // 1. Resolve handheld device (matched by serial/name, or first active handheld)
            HandheldDevice? handheld = null;
            if (!string.IsNullOrWhiteSpace(request.handheldDeviceSerial))
            {
                handheld = await _db.HandheldDevices
                    .FirstOrDefaultAsync(h => !h.IsDeleted && (h.DeviceSerial.ToLower() == request.handheldDeviceSerial.ToLower() || h.Name.ToLower() == request.handheldDeviceSerial.ToLower()));
            }
            if (handheld == null)
            {
                handheld = await _db.HandheldDevices
                    .FirstOrDefaultAsync(h => !h.IsDeleted);
            }
            Guid? handheldGuid = handheld?.Id;

            // 2. Resolve location: custom typed location auto-creates if new, blank input falls back to existing DB location
            Location? resolvedLocation = null;

            if (!string.IsNullOrWhiteSpace(request.location) && !request.location.StartsWith("GPS", StringComparison.OrdinalIgnoreCase))
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
                        (siteId == Guid.Empty || l.Zone.Warehouse.SiteId == siteId) &&
                        (l.Name.ToLower() == locQuery.ToLower() ||
                         l.Code.ToLower() == locQuery.ToLower() ||
                         l.Name.ToLower().Contains(locQuery.ToLower()) ||
                         l.Code.ToLower().Contains(locQuery.ToLower())));

                // Auto-create new location if custom location specified from handheld (e.g., "bay 777") is not yet in DB
                if (resolvedLocation == null && !string.IsNullOrWhiteSpace(locQuery))
                {
                    var defaultZone = await _db.Zones
                        .Include(z => z.Warehouse)
                        .FirstOrDefaultAsync(z => siteId == Guid.Empty || z.Warehouse.SiteId == siteId);

                    if (defaultZone == null)
                    {
                        var targetSiteId = siteId;
                        if (targetSiteId == Guid.Empty)
                        {
                            var site = await _db.Sites.FirstOrDefaultAsync();
                            targetSiteId = site?.Id ?? Guid.NewGuid();
                        }

                        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(w => w.SiteId == targetSiteId);
                        if (warehouse == null)
                        {
                            warehouse = new Warehouse
                            {
                                Id = Guid.NewGuid(),
                                SiteId = targetSiteId,
                                Name = "Main Warehouse",
                                Code = "WH-MAIN",
                                CreatedOn = DateTime.UtcNow
                            };
                            _db.Warehouses.Add(warehouse);
                            await _db.SaveChangesAsync();
                        }

                        defaultZone = new Zone
                        {
                            Id = Guid.NewGuid(),
                            WarehouseId = warehouse.Id,
                            Name = "General Storage Zone",
                            Code = "ZONE-GEN",
                            CreatedOn = DateTime.UtcNow
                        };
                        _db.Zones.Add(defaultZone);
                        await _db.SaveChangesAsync();
                    }

                    var locCode = "LOC-" + System.Text.RegularExpressions.Regex.Replace(locQuery, @"\s+", "-").ToUpper();
                    resolvedLocation = new Location
                    {
                        Id = Guid.NewGuid(),
                        ZoneId = defaultZone.Id,
                        Name = locQuery,
                        Code = locCode,
                        CreatedOn = DateTime.UtcNow
                    };
                    _db.Locations.Add(resolvedLocation);
                    await _db.SaveChangesAsync();
                }
            }

            // Coordinates lookup fallback against existing DB locations:
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
                var locationsWithCoords = await _db.Locations
                    .Include(l => l.Zone).ThenInclude(z => z.Warehouse)
                    .Where(l => l.Latitude != null && l.Longitude != null && (siteId == Guid.Empty || l.Zone.Warehouse.SiteId == siteId))
                    .ToListAsync();

                Location? closestLocation = null;
                double minDistance = double.MaxValue;

                foreach (var loc in locationsWithCoords)
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

            // Final fallback: Always map to an existing registered Location in the database
            if (resolvedLocation == null)
            {
                resolvedLocation = await _db.Locations
                    .Include(l => l.Zone).ThenInclude(z => z.Warehouse)
                    .FirstOrDefaultAsync(l => siteId == Guid.Empty || l.Zone.Warehouse.SiteId == siteId)
                    ?? await _db.Locations.Include(l => l.Zone).ThenInclude(z => z.Warehouse).FirstOrDefaultAsync();
            }

            var resolvedLocationId = resolvedLocation?.Id;
            var resolvedLocationName = resolvedLocation?.Name ?? "Pune DC Main Warehouse";

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
                Guid? originalLocationId = null;
                var tag = await _db.RFIDTags.FirstOrDefaultAsync(t =>
                    t.EpcCode.ToLower() == epcClean.ToLower());

                if (tag == null)
                {
                    var cleanEpc = epcClean.Replace(" ", "").ToUpper();
                    var assetSuffix = cleanEpc.Length >= 6 ? cleanEpc.Substring(cleanEpc.Length - 6) : cleanEpc;
                    var cat = await _db.AssetCategories.FirstOrDefaultAsync();
                    var defaultCatId = cat?.Id ?? Guid.Parse("019f3c62-cbca-75ec-a0a1-568c034f1200");

                    var newAsset = new Asset
                    {
                        Id = Guid.NewGuid(),
                        AssetNumber = $"AST-{assetSuffix}",
                        Name = $"Scanned Asset ({assetSuffix})",
                        AssetCategoryId = defaultCatId,
                        Status = Domain.Enums.AssetStatus.Available,
                        LocationId = resolvedLocationId,
                        SiteId = siteId != Guid.Empty ? siteId : (resolvedLocation?.Zone?.Warehouse?.SiteId ?? Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c91")),
                        CreatedOn = DateTime.UtcNow
                    };
                    _db.Assets.Add(newAsset);

                    tag = new RFIDTag
                    {
                        Id = Guid.NewGuid(),
                        EpcCode = epcClean,
                        AssetId = newAsset.Id,
                        Status = Domain.Enums.TagStatus.Active,
                        CreatedOn = DateTime.UtcNow
                    };
                    _db.RFIDTags.Add(tag);
                    await _db.SaveChangesAsync();
                }

                if (tag != null && tag.AssetId != null)
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

                        // Automatically update any Missing or CheckedOut status in Check-In / Check-Out (AssetAssignments)
                        var assignmentsToUpdate = await _db.AssetAssignments
                            .Where(a => a.AssetId == asset.Id && (a.Status == "Missing" || a.Status == "CheckedOut" || a.ActualReturnDate == null))
                            .ToListAsync();

                        foreach (var assign in assignmentsToUpdate)
                        {
                            assign.Status = "Completed";
                            assign.ActualReturnDate = DateTime.UtcNow;
                            assign.Notes = (assign.Notes ?? "") + $" | Auto-completed via Handheld Inventory Scan at {resolvedLocationName}";
                            _db.AssetAssignments.Update(assign);
                        }

                        // Also update any TruckEquipmentAssignments linked to this asset/equipment
                        var truckAssignmentsToUpdate = await _db.TruckEquipmentAssignments
                            .Where(ta => ta.EquipmentId == asset.Id && ta.ReturnedAt == null)
                            .ToListAsync();

                        foreach (var ta in truckAssignmentsToUpdate)
                        {
                            ta.Status = "Completed";
                            ta.ReturnedAt = DateTime.UtcNow;
                            _db.TruckEquipmentAssignments.Update(ta);
                        }

                        // Also recover missing equipment cases linked to this asset/EPC
                        var openCases = await _db.MissingEquipmentCases
                            .Include(c => c.Items)
                            .Where(c => c.ClosedAt == null)
                            .ToListAsync();

                        foreach (var c in openCases)
                        {
                            foreach (var mi in c.Items.Where(i => !i.IsRecovered && (i.EquipmentId == asset.Id || i.Epc.ToLower() == epcClean.ToLower())))
                            {
                                mi.IsRecovered = true;
                                mi.RecoveredAt = DateTime.UtcNow;
                            }
                            if (!c.Items.Any(i => !i.IsRecovered))
                            {
                                c.ClosedAt = DateTime.UtcNow;
                            }
                        }

                        // Update asset status to Available if it was in any non-available state
                        if (asset.Status != Domain.Enums.AssetStatus.Available)
                        {
                            asset.Status = Domain.Enums.AssetStatus.Available;
                            _db.Assets.Update(asset);
                        }

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
        [HttpGet("inventory-scans")]
        public async Task<ActionResult> GetInventoryScannedItems()
        {
            var movements = await _db.AssetMovements
                .Include(m => m.Asset).ThenInclude(a => a.AssetCategory)
                .Include(m => m.Asset).ThenInclude(a => a.Site)
                .Include(m => m.Asset).ThenInclude(a => a.Location)
                .Include(m => m.DestinationLocation)
                .Include(m => m.HandheldDevice)
                .Where(m => !m.IsDeleted && (m.MovementType == "HandheldInventory" || m.MovementType == "ScanInventory"))
                .OrderByDescending(m => m.MovementDate)
                .ToListAsync();

            var tags = await _db.RFIDTags.Where(t => !t.IsDeleted).ToListAsync();

            var items = movements
                .GroupBy(m => m.AssetId)
                .Select(g =>
                {
                    var latest = g.First();
                    var asset = latest.Asset;
                    var tag = tags.FirstOrDefault(t => t.AssetId == asset.Id);
                    return new
                    {
                        id = asset.Id,
                        sku = asset.AssetNumber,
                        name = asset.Name,
                        category = asset.AssetCategory?.Name ?? "General",
                        rfidTag = tag?.EpcCode ?? "—",
                        expectedQty = 1,
                        actualQty = asset.Status == Domain.Enums.AssetStatus.Retired ? 0 : 1,
                        unit = "unit",
                        zone = asset.Site?.Name ?? "Pune DC",
                        binLocation = latest.DestinationLocation?.Name ?? asset.Location?.Name ?? "Staging Area",
                        status = asset.Status == Domain.Enums.AssetStatus.Retired ? "Missing" : "In Stock",
                        lastAuditTime = latest.MovementDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                        checkedBy = latest.HandheldDevice?.Name ?? "Android Handheld"
                    };
                })
                .ToList();

            return Ok(items);
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
        public async Task<ActionResult> GetReaderList([FromQuery] string siteId, [FromQuery] string? scanMode = null, [FromQuery] string? deviceId = null)
        {
            if (!Guid.TryParse(siteId, out var siteGuid))
            {
                return BadRequest("Invalid siteId format");
            }

            // 1. If deviceId is provided, look for matching Handheld Device first
            if (!string.IsNullOrEmpty(deviceId))
            {
                HandheldDevice? handheld = null;
                if (Guid.TryParse(deviceId, out var parsedGuid))
                {
                    handheld = await _db.HandheldDevices.FirstOrDefaultAsync(h => !h.IsDeleted && h.Id == parsedGuid);
                }
                if (handheld == null)
                {
                    handheld = await _db.HandheldDevices.FirstOrDefaultAsync(h => !h.IsDeleted && (h.DeviceSerial.ToLower() == deviceId.ToLower() || h.Name.ToLower() == deviceId.ToLower()));
                }

                if (handheld != null)
                {
                    return Ok(new List<object>
                    {
                        new
                        {
                            readerId = handheld.Id.ToString(),
                            name = handheld.Name,
                            direction = scanMode,
                            siteId = siteGuid.ToString()
                        }
                    });
                }
            }

            var query = _db.Readers.Where(r => r.SiteId == siteGuid);

            if (!string.IsNullOrEmpty(scanMode))
            {
                query = query.Where(r => r.Direction != null && r.Direction.ToUpper() == scanMode.ToUpper());
            }

            var readers = await query.ToListAsync();

            if (!readers.Any() && !string.IsNullOrEmpty(scanMode))
            {
                readers = await _db.Readers.Where(r => r.SiteId == siteGuid).ToListAsync();
            }

            if (!readers.Any())
            {
                // Fallback to handheld devices
                var handhelds = await _db.HandheldDevices
                    .Where(h => !h.IsDeleted)
                    .Select(h => new
                    {
                        readerId = h.Id.ToString(),
                        name = h.Name,
                        direction = (string?)null,
                        siteId = siteGuid.ToString()
                    })
                    .ToListAsync();
                return Ok(handhelds);
            }

            var result = readers.Select(r => new
            {
                readerId = r.Id.ToString(),
                name = r.Name,
                direction = r.Direction,
                siteId = r.SiteId.ToString()
            });

            return Ok(result);
        }

        [HttpPost("/api/admin/users/save-driver")]
        [AllowAnonymous]
        public async Task<ActionResult> SaveDriver([FromBody] RfidSaveDriverRequest request)
        {
            try
            {
                var entityType = string.IsNullOrWhiteSpace(request.Type) ? "Driver" : request.Type.Trim();
                if (entityType.Equals("Driver-Based", StringComparison.OrdinalIgnoreCase)) entityType = "Driver";

                var existing = await _db.Drivers
                    .FirstOrDefaultAsync(d => d.FullName.ToLower() == request.FullName.ToLower() && !d.IsDeleted);

                if (existing == null)
                {
                    var driver = new Driver
                    {
                        Id = Guid.NewGuid(),
                        FullName = request.FullName,
                        Email = $"Type:{entityType}",
                        CreatedOn = DateTime.UtcNow
                    };
                    _db.Drivers.Add(driver);
                    await _db.SaveChangesAsync();
                }
                else
                {
                    existing.Email = $"Type:{entityType}";
                    _db.Drivers.Update(existing);
                    await _db.SaveChangesAsync();
                }

                return Ok(new { success = true, message = "Saved successfully" });
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
        public string? handheldDeviceSerial { get; set; }
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
        public object? AntennaId { get; set; } // Antenna port / index at batch level (string "3" or int 3)
        public object? AntennaPort { get; set; }
        public object? AntennaIndex { get; set; }
        public string? SiteId { get; set; }
        public string? ScanMode { get; set; } // Support handheld checkin/checkout modes
        public string? OperatorName { get; set; }
        public string? type { get; set; }
        public List<RfidEvent> Events { get; set; } = new();

        public int? GetParsedAntennaId()
        {
            if (AntennaId != null && int.TryParse(AntennaId.ToString(), out int aId)) return aId;
            if (AntennaPort != null && int.TryParse(AntennaPort.ToString(), out int aPort)) return aPort;
            if (AntennaIndex != null && int.TryParse(AntennaIndex.ToString(), out int aIdx)) return aIdx;
            return null;
        }
    }

    public class RfidEvent
    {
        public Guid ScanId { get; set; }
        public string Epc { get; set; } = null!;
        public double Rssi { get; set; }
        public DateTime Timestamp { get; set; }
        public string type { get; set; } = null!;
        public object? AntennaId { get; set; } // Antenna port / index per event (string "3" or int 3)
        public object? AntennaPort { get; set; }
        public object? AntennaIndex { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Speed { get; set; }
        public double? Bearing { get; set; }

        public int? GetParsedAntennaId()
        {
            if (AntennaId != null && int.TryParse(AntennaId.ToString(), out int aId)) return aId;
            if (AntennaPort != null && int.TryParse(AntennaPort.ToString(), out int aPort)) return aPort;
            if (AntennaIndex != null && int.TryParse(AntennaIndex.ToString(), out int aIdx)) return aIdx;
            return null;
        }
    }
}
