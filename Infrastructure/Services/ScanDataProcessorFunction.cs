using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;
using Domain.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Alert = AssetTracking.Rfid.Domain.Entities.Alert;

namespace AssetTracking.Rfid.ScanProcessor;

public class ScanDataProcessorFunction
{
    private readonly ILogger<ScanDataProcessorFunction> _logger;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public ScanDataProcessorFunction(
        ILogger<ScanDataProcessorFunction> logger,
        AppDbContext db,
        IConfiguration config)
    {
        _logger = logger;
        _db = db;
        _config = config;
    }

    [Function("ScanDataProcessor")]
    public async Task RunAsync([TimerTrigger("*/30 * * * * *")] TimerInfo timer)
    {
        var now = DateTime.UtcNow;
        var batchSize = _config.GetValue<int>("ScanProcessor:BatchSize", 500);
        var bufferSeconds = _config.GetValue<int>("ScanProcessor:BufferSeconds", 30);
        var until = now.AddSeconds(-bufferSeconds);

        // 1. Fetch unprocessed scans in batches
        var scans = await _db.RfidScans
            .Where(s => s.ProcessedAt == null && s.Timestamp <= until)
            .OrderBy(s => s.Timestamp)
            .Take(batchSize)
            .ToListAsync();

        if (!scans.Any()) return;

        _logger.LogInformation("Processing batch of {Count} scans.", scans.Count);

        // 2. Group by Site and Reader for isolated processing
        var scansBySiteReader = scans.GroupBy(s => new { s.SiteId, s.ReaderId });

        foreach (var group in scansBySiteReader)
        {
            if (!Guid.TryParse(group.Key.SiteId, out var siteGuid))
            {
                _logger.LogWarning("Invalid SiteId format in scans: {SiteId}", group.Key.SiteId);
                continue;
            }

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                await ProcessReaderScansAsync(siteGuid, group.Key.ReaderId, group.ToList(), now);

                // Mark current group of scans as processed within the transaction
                foreach (var scan in group)
                {
                    scan.ProcessedAt = now;
                }
                await _db.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error processing scans for Site {SiteId}, Reader {ReaderId}", group.Key.SiteId, group.Key.ReaderId);
            }
        }

        _logger.LogInformation("Finished processing batch of {Count} scans.", scans.Count);
    }

    private async Task ProcessReaderScansAsync(Guid siteId, string readerId, List<RfidScan> scans, DateTime now)
    {
        if (!Guid.TryParse(readerId, out var readerGuid))
        {
            _logger.LogWarning("Invalid ReaderId format: {ReaderId}", readerId);
            return;
        }

        var reader = await _db.Readers.FirstOrDefaultAsync(r => r.Id == readerGuid && r.SiteId == siteId);
        HandheldDevice? handheld = null;
        if (reader == null)
        {
            handheld = await _db.HandheldDevices.FirstOrDefaultAsync(h => h.Id == readerGuid);
            if (handheld == null)
            {
                _logger.LogWarning("Reader or Handheld {ReaderId} not found for Site {SiteId}", readerId, siteId);
                return;
            }
        }

        // 4. Debounce: Ignore same tag within 5 seconds for live environments
        var debouncedScans = DebounceScans(scans, TimeSpan.FromSeconds(5));

        // 5. Group into Sessions: 30-60 second time window
        var sessions = GroupIntoSessions(debouncedScans, TimeSpan.FromSeconds(45));

        foreach (var session in sessions)
        {
            await ProcessSingleSessionAsync(siteId, reader, handheld, session, now);
        }
    }

    private async Task ProcessSingleSessionAsync(Guid siteId, Reader? reader, HandheldDevice? handheld, ScanSession session, DateTime now)
    {
        var scannedEpcs = session.Scans
            .Select(s => s.Epc?.Trim().ToUpperInvariant())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Cast<string>()
            .Distinct()
            .ToList();

        if (!scannedEpcs.Any()) return;

        var epcToType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var scan in session.Scans)
        {
            var epc = scan.Epc?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(epc) && !string.IsNullOrEmpty(scan.type) && !epcToType.ContainsKey(epc))
            {
                epcToType[epc] = scan.type;
            }
        }

        // 6. Identify Entities
        var (scannedTruck, equipments) = await IdentifyEntitiesAsync(scannedEpcs, siteId);

        Guid deviceId = reader != null ? reader.Id : handheld!.Id;
        Guid readerIdForGate = reader != null ? reader.ReaderId : handheld!.Id;

        var activeSession = await _db.ActiveTruckSessions
            .FirstOrDefaultAsync(x => x.ReaderId == deviceId && x.SiteId == siteId);

        // Resolve Direction
        string direction = "ENTRY";
        if (reader != null)
        {
            direction = reader.Direction?.Trim().ToUpperInvariant() ?? "ENTRY";
            if (direction == "BOTH")
            {
                var dirTruckId = scannedTruck?.TruckId ?? activeSession?.TruckId;
                direction = await ResolveBothDirectionAsync(dirTruckId, equipments, siteId);
            }
        }
        else
        {
            var firstScan = session.Scans.FirstOrDefault();
            if (firstScan != null && firstScan.type != null)
            {
                if (firstScan.type.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    direction = "EXIT";
                }
                else if (firstScan.type.IndexOf("Entry", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    direction = "ENTRY";
                }
            }
        }

        var isCheckout = direction == "ENTRY";
        var eventType = isCheckout ? "Entry" : "Exit";

        // 7. Flow Logic: Checkout (Entry) / Checkin (Exit)
        if (scannedTruck != null)
        {
            // Case A: A new truck has been physically scanned in this session

            // NEW TRUCK ARRIVED: Finalize previous if different
            if (activeSession != null && activeSession.TruckId != scannedTruck.TruckId)
            {
                await FinalizePreviousTruckSessionAsync(activeSession, siteId, readerIdForGate, now);
            }

            // Check if truck already processed today for this direction to avoid duplicates
            if (await IsAlreadyProcessedTodayAsync(scannedTruck.TruckId, siteId, eventType, now))
            {
                _logger.LogInformation("Truck {TruckNumber} already {EventType} today. Skipping duplicate event.", scannedTruck.TruckNumber, eventType);
                return;
            }

            var gateEvent = new GateEvent
            {
                GateEventId = Guid.NewGuid(),
                TruckId = scannedTruck.TruckId,
                DriverId = scannedTruck.DriverId,
                ReaderId = readerIdForGate,
                SiteId = siteId,
                EventTime = session.End,
                EventType = eventType,
                Status = "Completed"
            };
            _db.GateEvents.Add(gateEvent);

            if (activeSession == null)
            {
                activeSession = new ActiveTruckSession { Id = Guid.NewGuid(), ReaderId = deviceId, SiteId = siteId };
                _db.ActiveTruckSessions.Add(activeSession);
            }

            activeSession.TruckId = scannedTruck.TruckId;
            activeSession.DriverId = scannedTruck.DriverId;
            activeSession.GateEventId = gateEvent.GateEventId;
            activeSession.LastUpdated = now;

            await _db.SaveChangesAsync();

            // Handle Assignments and Equipment Timestamps
            await UpdateAssignmentsAndEquipmentAsync(gateEvent, equipments, isCheckout, now, epcToType);

            // If CHECKIN, handle missing logic immediately
            if (!isCheckout)
            {
                await HandleMissingEquipmentLogicAsync(scannedTruck, activeSession.DriverId, scannedEpcs, siteId, now, gateEvent.GateEventId);
            }
        }
        else if (equipments.Any())
        {
            // Case B: Only equipment is scanned in this session (no truck tag in the current scans)
            Guid? associatedTruckId = activeSession?.TruckId;
            Guid? associatedGateEventId = activeSession?.GateEventId;
            Guid? associatedDriverId = activeSession?.DriverId;

            // Fallback: if no active session but scans are of type "Reader" or "Handheld", find the most recent truck event at this reader/site
            if (associatedTruckId == null && associatedDriverId == null)
            {
                var lastTruckEvent = await _db.GateEvents
                    .Where(g => g.SiteId == siteId && g.ReaderId == readerIdForGate && (g.TruckId != null || g.DriverId != null))
                    .OrderByDescending(g => g.EventTime)
                    .FirstOrDefaultAsync();

                if (lastTruckEvent != null)
                {
                    associatedTruckId = lastTruckEvent.TruckId;
                    associatedGateEventId = lastTruckEvent.GateEventId;
                    associatedDriverId = lastTruckEvent.DriverId;
                }
            }

            if (associatedGateEventId != null)
            {
                // Append equipment scans to the active/most recent truck/driver event
                await AddItemsToActiveEventAsync(associatedGateEventId.Value, associatedTruckId, associatedDriverId, equipments, siteId, session.End, isCheckout, epcToType);
            }
            else if (associatedTruckId != null || associatedDriverId != null)
            {
                // First batch for a manually initialized session: Create a new GateEvent for this truck/driver
                var gateEvent = new GateEvent
                {
                    GateEventId = Guid.NewGuid(),
                    TruckId = associatedTruckId,
                    DriverId = associatedDriverId,
                    ReaderId = readerIdForGate,
                    SiteId = siteId,
                    EventTime = session.End,
                    EventType = eventType,
                    Status = "Completed"
                };
                _db.GateEvents.Add(gateEvent);

                if (activeSession != null)
                {
                    activeSession.GateEventId = gateEvent.GateEventId;
                    activeSession.LastUpdated = now;
                }
                await _db.SaveChangesAsync();

                await UpdateAssignmentsAndEquipmentAsync(gateEvent, equipments, isCheckout, now, epcToType);

                // For Check-In, handle missing logic
                if (!isCheckout)
                {
                    var truckObj = associatedTruckId.HasValue
                        ? await _db.Trucks.FindAsync(associatedTruckId.Value)
                        : null;
                    await HandleMissingEquipmentLogicAsync(truckObj, associatedDriverId, scannedEpcs, siteId, now, gateEvent.GateEventId);
                }
            }
            else
            {
                // Standalone equipment scans (e.g. independent tracking/goats)
                var gateEvent = new GateEvent
                {
                    GateEventId = Guid.NewGuid(),
                    TruckId = null,
                    ReaderId = readerIdForGate,
                    SiteId = siteId,
                    EventTime = session.End,
                    EventType = eventType,
                    Status = "Completed"
                };
                _db.GateEvents.Add(gateEvent);
                await _db.SaveChangesAsync();

                await UpdateAssignmentsAndEquipmentAsync(gateEvent, equipments, isCheckout, now, epcToType);
            }
        }

        // 8. Recovery Logic (Always run): check if any equipment being scanned is part of an open case
        await RecoverCasesAsync(scannedEpcs, siteId, now, isCheckout);
    }

    private async Task UpdateAssignmentsAndEquipmentAsync(GateEvent gate, List<Equipment> equipments, bool isCheckout, DateTime now, Dictionary<string, string> epcToType)
    {
        foreach (var eq in equipments)
        {
            var epc = eq.RfidTag?.TagName?.ToUpperInvariant() ?? "";
            var scanType = epcToType.GetValueOrDefault(epc, "EQUIPMENT");

            // Update Equipment Timestamps
            if (isCheckout) eq.LastDateTimeOut = now;
            else eq.LastDateTimeIn = now;

            // Update Assignments
            var existingAssignment = await _db.TruckEquipmentAssignments
                .FirstOrDefaultAsync(a => 
                    (gate.TruckId != null ? a.TruckId == gate.TruckId : a.DriverId == gate.DriverId) && 
                    a.EquipmentId == eq.EquipmentId && 
                    a.ReturnedAt == null);

            if (isCheckout)
            {
                if (existingAssignment == null)
                {
                    _db.TruckEquipmentAssignments.Add(new TruckEquipmentAssignment
                    {
                        AssignmentId = Guid.NewGuid(),
                        TruckId = gate.TruckId,
                        DriverId = gate.DriverId,
                        EquipmentId = eq.EquipmentId,
                        AssignedAt = now,
                        SiteId = gate.SiteId,
                        Status = "OUT",
                        Type = scanType
                    });
                }
            }
            else
            {
                if (existingAssignment != null)
                {
                    existingAssignment.ReturnedAt = now;
                    existingAssignment.Status = "IN";
                    existingAssignment.Type = scanType;
                }
            }

            // Sync with legacy AssetAssignment
            var adminUser = await _db.Users.FirstOrDefaultAsync();
            if (adminUser != null)
            {
                if (isCheckout)
                {
                    var existingLegacy = await _db.AssetAssignments
                        .FirstOrDefaultAsync(a => a.AssetId == eq.EquipmentId && a.ActualReturnDate == null);

                    if (existingLegacy == null)
                    {
                        var custodian = gate.TruckId != null 
                            ? (await _db.Trucks.FindAsync(gate.TruckId))?.TruckNumber 
                            : (gate.DriverId != null ? (await _db.Drivers.FindAsync(gate.DriverId))?.FullName : null);

                        _db.AssetAssignments.Add(new AssetAssignment
                        {
                            Id = Guid.NewGuid(),
                            AssetId = eq.EquipmentId,
                            AssignedToUserId = adminUser.Id,
                            CustodianName = custodian ?? "System RFID Scan",
                            AssignedDate = now,
                            Status = "Active",
                            Notes = "Auto-created via RFID Gate checkout"
                        });
                    }
                }
                else
                {
                    var existingLegacy = await _db.AssetAssignments
                        .FirstOrDefaultAsync(a => a.AssetId == eq.EquipmentId && a.ActualReturnDate == null);

                    if (existingLegacy != null)
                    {
                        existingLegacy.ActualReturnDate = now;
                        existingLegacy.Status = "Returned";
                        existingLegacy.Notes += "; Auto-returned via RFID Gate checkin";
                    }
                }
            }

            // Add Gate Event Item
            _db.GateEventItems.Add(new GateEventItem
            {
                GateEventItemId = Guid.NewGuid(),
                GateEventId = gate.GateEventId,
                EquipmentId = eq.EquipmentId,
                Epc = eq.RfidTag?.TagName ?? "",
                EventTime = gate.EventTime,
                SiteId = gate.SiteId,
                Type = scanType
            });
        }
    }

    private async Task HandleMissingEquipmentLogicAsync(Truck? truck, Guid? driverId, List<string> checkinEpcs, Guid siteId, DateTime now, Guid currentGateEventId)
    {
        if (truck == null && !driverId.HasValue) return;

        // Find last checkout (Entry) for this entity (truck or driver/individual) at this site
        GateEvent? lastCheckout = null;
        if (truck != null)
        {
            lastCheckout = await _db.GateEvents
                .Include(g => g.Items)
                .Where(g => g.TruckId == truck.TruckId && g.EventType == "Entry" && g.SiteId == siteId)
                .OrderByDescending(g => g.EventTime)
                .FirstOrDefaultAsync();
        }
        else if (driverId.HasValue)
        {
            lastCheckout = await _db.GateEvents
                .Include(g => g.Items)
                .Where(g => g.DriverId == driverId.Value && g.EventType == "Entry" && g.SiteId == siteId)
                .OrderByDescending(g => g.EventTime)
                .FirstOrDefaultAsync();
        }

        if (lastCheckout == null) return;

        var checkoutEpcs = lastCheckout.Items
            .Select(i => i.Epc?.ToUpper())
            .Where(e => e != null)
            .Cast<string>()
            .ToHashSet();

        var missingEpcs = checkoutEpcs.Except(checkinEpcs.Select(e => e.ToUpper())).ToList();
        if (!missingEpcs.Any()) return;

        // Deduplication: Only create case items for EPCs that don't already have an open case for this truck/driver
        List<string> openCasesForEntity = new List<string>();
        if (truck != null)
        {
            openCasesForEntity = await _db.MissingEquipmentCaseItems
                .Where(i => i.MissingEquipmentCase != null &&
                            i.MissingEquipmentCase.TruckId == truck.TruckId &&
                            i.MissingEquipmentCase.ClosedAt == null &&
                            !i.IsRecovered)
                .Select(i => i.Epc != null ? i.Epc.ToUpper() : "")
                .ToListAsync();
        }
        else if (driverId.HasValue)
        {
            openCasesForEntity = await _db.MissingEquipmentCaseItems
                .Where(i => i.MissingEquipmentCase != null &&
                            i.MissingEquipmentCase.DriverId == driverId.Value &&
                            i.MissingEquipmentCase.ClosedAt == null &&
                            !i.IsRecovered)
                .Select(i => i.Epc != null ? i.Epc.ToUpper() : "")
                .ToListAsync();
        }

        var trulyMissingEpcs = missingEpcs.Where(e => !openCasesForEntity.Contains(e.ToUpper())).ToList();

        if (!trulyMissingEpcs.Any()) return;

        var openStatusId = await GetOrCreateStatusIdAsync("Open");
        var severityId = await GetOrCreateSeverityIdAsync(0);

        var missingCase = new MissingEquipmentCase
        {
            MissingEquipmentCaseId = Guid.NewGuid(),
            TruckId = truck?.TruckId,
            DriverId = driverId,
            SiteId = siteId,
            OpenedAt = now,
            StatusId = openStatusId,
            SeverityId = severityId,
            Items = new List<MissingEquipmentCaseItem>()
        };

        foreach (var epc in trulyMissingEpcs)
        {
            var eq = await _db.Equipment
                .Include(e => e.RfidTag)
                .FirstOrDefaultAsync(e => e.RfidTag != null && e.RfidTag.TagName == epc && e.SiteId == siteId);

            if (eq != null)
            {
                missingCase.Items.Add(new MissingEquipmentCaseItem
                {
                    MissingEquipmentCaseItemId = Guid.NewGuid(),
                    MissingEquipmentCaseId = missingCase.MissingEquipmentCaseId,
                    EquipmentId = eq.EquipmentId,
                    Epc = epc,
                    StatusId = openStatusId,
                    SiteId = siteId,
                    IsRecovered = false
                });
            }
        }

        if (missingCase.Items.Any())
        {
            _db.MissingEquipmentCases.Add(missingCase);

            string entityName = "Unknown";
            if (truck != null)
            {
                entityName = $"Truck {truck.TruckNumber}";
            }
            else if (driverId.HasValue)
            {
                var driver = await _db.Drivers.FindAsync(driverId.Value);
                entityName = driver != null ? $"Driver/Individual {driver.FullName}" : "Driver/Individual";
            }

            // Alert
            _db.Alerts.Add(new Alert
            {
                AlertId = Guid.NewGuid(),
                Timestamp = now,
                Severity = "High",
                Source = "ScanProcessor",
                SiteId = siteId,
                Message = $"{entityName} checked in with {trulyMissingEpcs.Count} missing items. Case {missingCase.MissingEquipmentCaseId} opened."
            });
            await _db.SaveChangesAsync();
        }
    }

    private async Task RecoverCasesAsync(List<string> scannedEpcs, Guid siteId, DateTime now, bool isCheckout)
    {
        var openItems = await _db.MissingEquipmentCaseItems
            .Include(i => i.MissingEquipmentCase)
            .Where(i => i.MissingEquipmentCase != null &&
                        i.MissingEquipmentCase.SiteId == siteId &&
                        i.MissingEquipmentCase.ClosedAt == null &&
                        !i.IsRecovered &&
                        i.Epc != null &&
                        scannedEpcs.Contains(i.Epc.ToUpper()))
            .ToListAsync();

        if (!openItems.Any()) return;

        foreach (var item in openItems)
        {
            if (item.MissingEquipmentCase == null) continue;

            item.IsRecovered = true;
            item.RecoveredAt = now;
            item.StatusId = 4; // Explicitly requested by user

            // Auto-update assignment for the truck that had the missing item
            var assignment = await _db.TruckEquipmentAssignments
                .FirstOrDefaultAsync(a => a.TruckId == item.MissingEquipmentCase.TruckId && a.EquipmentId == item.EquipmentId && a.ReturnedAt == null);

            if (assignment != null && !isCheckout)
            {
                assignment.ReturnedAt = now;
                assignment.Status = "IN";
            }
        }
    }

    private async Task FinalizePreviousTruckSessionAsync(ActiveTruckSession activeSession, Guid siteId, Guid? readerId, DateTime now)
    {
        _logger.LogInformation("Finalizing previous session for Truck {TruckId} on Reader {ReaderId}", activeSession.TruckId, readerId);
        activeSession.TruckId = null;
        activeSession.DriverId = null;
        activeSession.GateEventId = null;
        activeSession.LastUpdated = now;
        await _db.SaveChangesAsync();
    }

    private async Task<bool> IsAlreadyProcessedTodayAsync(Guid truckId, Guid siteId, string eventType, DateTime now)
    {
        var today = now.Date;
        return await _db.GateEvents.AnyAsync(g =>
            g.TruckId == truckId &&
            g.SiteId == siteId &&
            g.EventType == eventType &&
            g.EventTime >= today);
    }

    private async Task<string> ResolveBothDirectionAsync(Guid? truckId, List<Equipment> equipments, Guid siteId)
    {
        if (truckId != null)
        {
            var lastEvent = await _db.GateEvents
                .Where(g => g.TruckId == truckId && g.SiteId == siteId)
                .OrderByDescending(g => g.EventTime)
                .Select(g => g.EventType)
                .FirstOrDefaultAsync();

            return lastEvent == "Entry" ? "EXIT" : "ENTRY";
        }
        else if (equipments.Any())
        {
            // For single entities, check the first one's last assignment/event
            var firstEqId = equipments.First().EquipmentId;
            var lastAssignment = await _db.TruckEquipmentAssignments
                .Where(a => a.EquipmentId == firstEqId && a.SiteId == siteId)
                .OrderByDescending(a => a.AssignedAt)
                .FirstOrDefaultAsync();

            // If currently OUT, we are likely checking in (EXIT)
            if (lastAssignment != null && lastAssignment.Status == "OUT" && lastAssignment.ReturnedAt == null)
            {
                return "EXIT";
            }
        }

        return "ENTRY";
    }

    private List<RfidScan> DebounceScans(List<RfidScan> scans, TimeSpan debounceTime)
    {
        var result = new List<RfidScan>();
        var lastSeen = new Dictionary<string, DateTimeOffset>();

        foreach (var scan in scans.OrderBy(s => s.Timestamp))
        {
            var epc = scan.Epc?.ToUpperInvariant();
            if (string.IsNullOrEmpty(epc)) continue;

            if (lastSeen.TryGetValue(epc, out var lastTime) && (scan.Timestamp - lastTime) < debounceTime)
            {
                continue;
            }

            lastSeen[epc] = scan.Timestamp;
            result.Add(scan);
        }
        return result;
    }

    private List<ScanSession> GroupIntoSessions(List<RfidScan> scans, TimeSpan window)
    {
        var sessions = new List<ScanSession>();
        ScanSession? current = null;

        foreach (var scan in scans.OrderBy(s => s.Timestamp))
        {
            if (current == null || (scan.Timestamp - current.End) > window)
            {
                current = new ScanSession
                {
                    Start = scan.Timestamp,
                    End = scan.Timestamp,
                    Scans = new List<RfidScan>()
                };
                sessions.Add(current);
            }
            current.Scans.Add(scan);
            current.End = scan.Timestamp;
        }
        return sessions;
    }

    private async Task<(Truck? truck, List<Equipment> equipments)> IdentifyEntitiesAsync(List<string> epcs, Guid siteId)
    {
        var truck = await _db.Trucks.Include(t => t.RfidTag)
            .FirstOrDefaultAsync(t => t.SiteId == siteId && t.RfidTag != null && t.RfidTag.TagName != null && epcs.Contains(t.RfidTag.TagName.ToUpper()));

        var equipments = await _db.Equipment.Include(e => e.RfidTag)
            .Where(e => e.SiteId == siteId && e.RfidTag != null && e.RfidTag.TagName != null && epcs.Contains(e.RfidTag.TagName.ToUpper()))
            .ToListAsync();

        return (truck, equipments);
    }

    private async Task AddItemsToActiveEventAsync(Guid gateEventId, Guid? truckId, Guid? driverId, List<Equipment> equipments, Guid siteId, DateTime eventTime, bool isCheckout, Dictionary<string, string> epcToType)
    {
        var gateEventExists = await _db.GateEvents.AnyAsync(g => g.GateEventId == gateEventId);
        if (!gateEventExists)
        {
            return;
        }

        var existingEpcs = await _db.GateEventItems
            .Where(i => i.GateEventId == gateEventId && i.Epc != null)
            .Select(i => i.Epc.ToUpperInvariant())
            .ToListAsync();

        foreach (var eq in equipments)
        {
            var epc = eq.RfidTag?.TagName?.ToUpperInvariant() ?? "";
            if (string.IsNullOrEmpty(epc) || existingEpcs.Contains(epc)) continue;
            var scanType = epcToType.GetValueOrDefault(epc, "EQUIPMENT");

            // Update Equipment Timestamps
            if (isCheckout) eq.LastDateTimeOut = eventTime;
            else eq.LastDateTimeIn = eventTime;

            // Update Assignments
            var existingAssignment = await _db.TruckEquipmentAssignments
                .FirstOrDefaultAsync(a => 
                    (truckId != null ? a.TruckId == truckId : a.DriverId == driverId) && 
                    a.EquipmentId == eq.EquipmentId && 
                    a.ReturnedAt == null);

            if (isCheckout)
            {
                if (existingAssignment == null)
                {
                    _db.TruckEquipmentAssignments.Add(new TruckEquipmentAssignment
                    {
                        AssignmentId = Guid.NewGuid(),
                        TruckId = truckId,
                        DriverId = driverId,
                        EquipmentId = eq.EquipmentId,
                        AssignedAt = eventTime,
                        SiteId = siteId,
                        Status = "OUT",
                        Type = scanType
                    });
                }
            }
            else
            {
                if (existingAssignment != null)
                {
                    existingAssignment.ReturnedAt = eventTime;
                    existingAssignment.Status = "IN";
                    existingAssignment.Type = scanType;
                }
            }

            // Sync with legacy AssetAssignment
            var adminUser = await _db.Users.FirstOrDefaultAsync();
            if (adminUser != null)
            {
                if (isCheckout)
                {
                    var existingLegacy = await _db.AssetAssignments
                        .FirstOrDefaultAsync(a => a.AssetId == eq.EquipmentId && a.ActualReturnDate == null);

                    if (existingLegacy == null)
                    {
                        var custodian = truckId != null 
                            ? (await _db.Trucks.FindAsync(truckId))?.TruckNumber 
                            : (driverId != null ? (await _db.Drivers.FindAsync(driverId))?.FullName : null);

                        _db.AssetAssignments.Add(new AssetAssignment
                        {
                            Id = Guid.NewGuid(),
                            AssetId = eq.EquipmentId,
                            AssignedToUserId = adminUser.Id,
                            CustodianName = custodian ?? "System RFID Scan",
                            AssignedDate = eventTime,
                            Status = "Active",
                            Notes = "Auto-created via RFID Gate checkout"
                        });
                    }
                }
                else
                {
                    var existingLegacy = await _db.AssetAssignments
                        .FirstOrDefaultAsync(a => a.AssetId == eq.EquipmentId && a.ActualReturnDate == null);

                    if (existingLegacy != null)
                    {
                        existingLegacy.ActualReturnDate = eventTime;
                        existingLegacy.Status = "Returned";
                        existingLegacy.Notes += "; Auto-returned via RFID Gate checkin";
                    }
                }
            }

            _db.GateEventItems.Add(new GateEventItem
            {
                GateEventItemId = Guid.NewGuid(),
                GateEventId = gateEventId,
                EquipmentId = eq.EquipmentId,
                Epc = epc,
                EventTime = eventTime,
                SiteId = siteId,
                Type = scanType
            });
        }
        await _db.SaveChangesAsync();
    }

    private async Task<int> GetOrCreateStatusIdAsync(string code)
    {
        var status = await _db.MissingEquipmentStatuses.FirstOrDefaultAsync(s => s.Code == code);
        if (status == null)
        {
            status = new MissingEquipmentStatus { Code = code, Description = $"Auto-seeded {code}", IsFinal = code == "Closed" };
            _db.MissingEquipmentStatuses.Add(status);
            await _db.SaveChangesAsync();
        }
        return status.StatusId;
    }

    private async Task<int> GetOrCreateSeverityIdAsync(decimal cost)
    {
        var severity = await _db.MissingEquipmentSeverities.OrderBy(s => s.Priority).FirstOrDefaultAsync();
        return severity?.SeverityId ?? 1;
    }

    private class ScanSession
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public List<RfidScan> Scans { get; set; } = new();
    }
}
