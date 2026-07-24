using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence.Context;
using Domain.Entities;
using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;
using Application.Interfaces;
using Application.Auth.Commands.Login;
using MediatR;
using RfidTag = AssetTracking.Rfid.Domain.Entities.RfidTag;
using Truck = AssetTracking.Rfid.Domain.Entities.Truck;
using Equipment = AssetTracking.Rfid.Domain.Entities.Equipment;
using Alert = AssetTracking.Rfid.Domain.Entities.Alert;

namespace API.Controllers
{
    [ApiController]
    public class LegacyCompatController : ControllerBase
    {
        [AllowAnonymous]
        [HttpGet("api/admin/users/download-apk")]
        public IActionResult DownloadApk()
        {
            var path = @"d:\RFID_Assettracking_new\AssetTracking handheld-rfid-android\handheld-rfid-android\AssetTrackingRfidProject\bin\Release\com.prosper.assettrackingrfid-Signed.apk";
            if (!System.IO.File.Exists(path))
            {
                path = @"d:\RFID_Assettracking_new\AssetTracking handheld-rfid-android\handheld-rfid-android\AssetTrackingRfidProject\bin\Debug\com.prosper.assettrackingrfid-Signed.apk";
            }

            if (!System.IO.File.Exists(path))
            {
                return NotFound("APK file not found on server");
            }

            var bytes = System.IO.File.ReadAllBytes(path);
            return File(bytes, "application/vnd.android.package-archive", "com.prosper.assettrackingrfid-Signed.apk");
        }



        [AllowAnonymous]
        [HttpGet("api/Trucks/sites")]
        public async Task<IActionResult> GetSites([FromServices] AppDbContext db)
        {
            var sites = await db.Sites.ToListAsync();
            var dropdownItems = sites.Select(s => new
            {
                id = s.Id.ToString(),
                text = s.Name
            });
            return Ok(dropdownItems);
        }

        [AllowAnonymous]
        [HttpGet("api/Trucks/trucksDropdown")]
        public async Task<IActionResult> GetTrucksDropdown([FromServices] AppDbContext db)
        {
            var trucks = await db.Trucks.ToListAsync();
            var dropdownItems = trucks.Select(t => new
            {
                id = t.TruckId.ToString(),
                text = t.TruckNumber
            });
            return Ok(dropdownItems);
        }

        [AllowAnonymous]
        [HttpGet("api/Trucks/drivers")]
        public async Task<IActionResult> GetDrivers([FromServices] AppDbContext db)
        {
            var drivers = await db.Drivers
                .Where(d => d.Email != "Type:Individual")
                .ToListAsync();

            var dropdownItems = drivers.Select(d => new
            {
                id = d.Id.ToString(),
                text = d.FullName
            });
            return Ok(dropdownItems);
        }

        [AllowAnonymous]
        [HttpGet("api/Trucks/individual")]
        public async Task<IActionResult> GetIndividuals([FromServices] AppDbContext db)
        {
            var individuals = await db.Drivers
                .Where(d => d.Email == "Type:Individual")
                .ToListAsync();

            var dropdownItems = individuals.Select(d => new
            {
                id = d.Id.ToString(),
                text = d.FullName
            });
            return Ok(dropdownItems);
        }

        [AllowAnonymous]
        [HttpGet("api/Trucks/activedrivers")]
        public async Task<IActionResult> GetActiveDrivers([FromServices] AppDbContext db)
        {
            var activeSessionDrivers = await db.ActiveTruckSessions
                .Where(s => s.DriverId != null)
                .Select(s => s.DriverId)
                .ToListAsync();

            var drivers = await db.Drivers
                .Where(d => activeSessionDrivers.Contains(d.Id) && d.Email != "Type:Individual")
                .ToListAsync();

            var dropdownItems = drivers.Select(d => new
            {
                id = d.Id.ToString(),
                text = d.FullName
            });
            return Ok(dropdownItems);
        }

        [AllowAnonymous]
        [HttpGet("api/Trucks/activeIndividual")]
        public async Task<IActionResult> GetActiveIndividuals([FromServices] AppDbContext db)
        {
            var activeSessionDrivers = await db.ActiveTruckSessions
                .Where(s => s.DriverId != null)
                .Select(s => s.DriverId)
                .ToListAsync();

            var individuals = await db.Drivers
                .Where(d => activeSessionDrivers.Contains(d.Id) && d.Email == "Type:Individual")
                .ToListAsync();

            var dropdownItems = individuals.Select(d => new
            {
                id = d.Id.ToString(),
                text = d.FullName
            });
            return Ok(dropdownItems);
        }

        [AllowAnonymous]
        [HttpGet("api/Trucks/activeTrucksDropdown")]
        public async Task<IActionResult> GetActiveTrucksDropdown([FromServices] AppDbContext db)
        {
            var activeSessionTrucks = await db.ActiveTruckSessions
                .Where(s => s.TruckId != null)
                .Select(s => s.TruckId)
                .ToListAsync();

            var trucks = await db.Trucks
                .Where(t => activeSessionTrucks.Contains(t.TruckId))
                .ToListAsync();

            var dropdownItems = trucks.Select(t => new
            {
                id = t.TruckId.ToString(),
                text = t.TruckNumber
            });
            return Ok(dropdownItems);
        }

        [AllowAnonymous]
        [HttpGet("api/Trucks/check-driver-assignment")]
        public async Task<IActionResult> CheckDriverAssignment([FromQuery] string driverName, [FromServices] AppDbContext db)
        {
            if (string.IsNullOrEmpty(driverName))
            {
                return Ok(new { assigned = false, truckNumber = "" });
            }

            var driver = await db.Drivers.FirstOrDefaultAsync(d => d.FullName.ToLower() == driverName.ToLower());
            if (driver == null)
            {
                return Ok(new { assigned = false, truckNumber = "" });
            }

            // Check if there is an active session or a truck referencing this driver
            var activeSession = await db.ActiveTruckSessions
                .FirstOrDefaultAsync(s => s.DriverId == driver.Id);

            if (activeSession != null && activeSession.TruckId != null)
            {
                var truck = await db.Trucks.FindAsync(activeSession.TruckId.Value);
                return Ok(new { assigned = true, truckNumber = truck?.TruckNumber ?? "Unknown" });
            }

            // Fallback: check if the driver is assigned to any truck in the Trucks table
            var assignedTruck = await db.Trucks.FirstOrDefaultAsync(t => t.DriverId == driver.Id);
            if (assignedTruck != null)
            {
                return Ok(new { assigned = true, truckNumber = assignedTruck.TruckNumber });
            }

            return Ok(new { assigned = false, truckNumber = "" });
        }

        [AllowAnonymous]
        [HttpPost("api/Trucks/ImportTruckCsv")]
        public async Task<IActionResult> ImportTruckCsv([FromBody] ImportTruckRequest request, [FromServices] AppDbContext db)
        {
            if (request?.rows == null || !request.rows.Any())
            {
                return BadRequest("No rows provided.");
            }

            if (!Guid.TryParse(request.siteId, out var siteGuid))
            {
                return BadRequest("Invalid siteId format.");
            }

            foreach (var row in request.rows)
            {
                // Find or create driver
                Guid? driverGuid = null;
                if (!string.IsNullOrEmpty(row.driverId) && Guid.TryParse(row.driverId, out var parsedDriverId))
                {
                    driverGuid = parsedDriverId;
                }
                else if (!string.IsNullOrEmpty(row.driverName))
                {
                    var driver = await db.Drivers.FirstOrDefaultAsync(d => d.FullName.ToLower() == row.driverName.ToLower());
                    if (driver == null)
                    {
                        driver = new Driver
                        {
                            Id = Guid.NewGuid(),
                            FullName = row.driverName,
                            CreatedOn = DateTime.UtcNow
                        };
                        db.Drivers.Add(driver);
                        await db.SaveChangesAsync();
                    }
                    driverGuid = driver.Id;
                }

                // Find or create RfidTag if provided
                Guid? rfidTagGuid = null;
                if (!string.IsNullOrEmpty(row.rfidTagId))
                {
                    if (Guid.TryParse(row.rfidTagId, out var parsedTagId))
                    {
                        rfidTagGuid = parsedTagId;
                    }
                    else
                    {
                        // Treat rfidTagId as tag name/EPC
                        var rfidTag = await db.CustomRfidTags.FirstOrDefaultAsync(t => t.TagName.ToLower() == row.rfidTagId.ToLower());
                        if (rfidTag == null)
                        {
                            rfidTag = new RfidTag
                            {
                                RfidTagId = Guid.NewGuid(),
                                TagName = row.rfidTagId
                            };
                            db.CustomRfidTags.Add(rfidTag);
                            await db.SaveChangesAsync();
                        }
                        rfidTagGuid = rfidTag.RfidTagId;
                    }
                }

                // Find or create Truck
                Guid truckGuid = Guid.NewGuid();
                if (!string.IsNullOrEmpty(row.truckId) && Guid.TryParse(row.truckId, out var parsedTruckId))
                {
                    truckGuid = parsedTruckId;
                }

                var truck = await db.Trucks.FirstOrDefaultAsync(t => t.TruckNumber.ToLower() == row.truckNumber.ToLower());
                if (truck == null)
                {
                    truck = new Truck
                    {
                        TruckId = truckGuid,
                        TruckNumber = row.truckNumber,
                        DriverId = driverGuid,
                        SiteId = siteGuid,
                        RfidTagId = rfidTagGuid
                    };
                    db.Trucks.Add(truck);
                }
                else
                {
                    truck.DriverId = driverGuid;
                    truck.SiteId = siteGuid;
                    if (rfidTagGuid != null)
                    {
                        truck.RfidTagId = rfidTagGuid;
                    }
                    db.Trucks.Update(truck);
                }
            }

            await db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [AllowAnonymous]
        [HttpPost("api/Trucks/initialize-session")]
        public async Task<IActionResult> InitializeSession([FromBody] InitializeSessionRequest request, [FromServices] AppDbContext db)
        {
            if (!Guid.TryParse(request.siteId, out var siteGuid))
            {
                return BadRequest("Invalid siteId format.");
            }

            if (!Guid.TryParse(request.readerId, out var readerGuid))
            {
                return BadRequest("Invalid readerId format.");
            }

            // Find or create driver
            Guid? driverGuid = null;
            if (!string.IsNullOrEmpty(request.driverId) && Guid.TryParse(request.driverId, out var parsedDriverId))
            {
                driverGuid = parsedDriverId;
            }
            else if (!string.IsNullOrEmpty(request.personName))
            {
                var driver = await db.Drivers.FirstOrDefaultAsync(d => d.FullName.ToLower() == request.personName.ToLower());
                if (driver == null)
                {
                    driver = new Driver
                    {
                        Id = Guid.NewGuid(),
                        FullName = request.personName,
                        CreatedOn = DateTime.UtcNow
                    };
                    db.Drivers.Add(driver);
                    await db.SaveChangesAsync();
                }
                driverGuid = driver.Id;
            }

            // Find or create truck if provided
            Guid? truckGuid = null;
            if (!string.IsNullOrEmpty(request.truckNumber))
            {
                var truck = await db.Trucks.FirstOrDefaultAsync(t => t.TruckNumber.ToLower() == request.truckNumber.ToLower());
                if (truck == null)
                {
                    truck = new Truck
                    {
                        TruckId = Guid.NewGuid(),
                        TruckNumber = request.truckNumber,
                        DriverId = driverGuid,
                        SiteId = siteGuid
                    };
                    db.Trucks.Add(truck);
                    await db.SaveChangesAsync();
                }
                truckGuid = truck.TruckId;
            }

            // Find or create ActiveTruckSession
            var activeSession = await db.ActiveTruckSessions
                .FirstOrDefaultAsync(s => s.ReaderId == readerGuid && s.SiteId == siteGuid);

            if (activeSession == null)
            {
                activeSession = new ActiveTruckSession
                {
                    Id = Guid.NewGuid(),
                    ReaderId = readerGuid,
                    SiteId = siteGuid,
                    TruckId = truckGuid,
                    DriverId = driverGuid,
                    LastUpdated = DateTime.UtcNow
                };
                db.ActiveTruckSessions.Add(activeSession);
            }
            else
            {
                activeSession.TruckId = truckGuid;
                activeSession.DriverId = driverGuid;
                activeSession.LastUpdated = DateTime.UtcNow;
                db.ActiveTruckSessions.Update(activeSession);
            }

            await db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [AllowAnonymous]
        [HttpGet("api/Trucks/{truckId:guid}")]
        public async Task<IActionResult> GetTruckDetails(Guid truckId, [FromServices] AppDbContext db)
        {
            var truck = await db.Trucks
                .Include(t => t.RfidTag)
                .FirstOrDefaultAsync(t => t.TruckId == truckId);

            if (truck == null)
            {
                return NotFound("Truck not found.");
            }

            string? driverName = null;
            if (truck.DriverId != null)
            {
                var driver = await db.Drivers.FindAsync(truck.DriverId.Value);
                driverName = driver?.FullName;
            }

            return Ok(new
            {
                truckId = truck.TruckId.ToString(),
                truckNumber = truck.TruckNumber,
                driverId = truck.DriverId?.ToString() ?? "",
                driverName = driverName ?? "",
                siteId = truck.SiteId.ToString(),
                rfidTagId = truck.RfidTagId?.ToString() ?? ""
            });
        }


        [AllowAnonymous]
        [HttpGet("api/Trucks/complete-status")]
        public async Task<IActionResult> GetCompleteStatus([FromServices] AppDbContext db)
        {
            // ── Pre-load all readers by direction or name ────────────────────────────
            var exitReaderIds = await db.Readers
                .Where(r => (r.Direction != null && r.Direction.ToUpper() == "EXIT") || (r.Name != null && r.Name.ToUpper().Contains("EXIT")))
                .Select(r => r.Id)
                .ToListAsync();

            var entryReaderIds = await db.Readers
                .Where(r => (r.Direction != null && r.Direction.ToUpper() == "ENTRY") || (r.Name != null && r.Name.ToUpper().Contains("ENTRY")))
                .Select(r => r.Id)
                .ToListAsync();

            // ── All EXIT movements (checkout) ────────────────────────────────────────
            var allExitMovements = await db.AssetMovements
                .Include(m => m.Asset)
                .Where(m => (m.ReaderId != null && exitReaderIds.Contains(m.ReaderId.Value)) || m.MovementType.ToUpper().Contains("CHECKOUT") || m.MovementType.ToUpper().Contains("EXIT"))
                .OrderByDescending(m => m.MovementDate)
                .ToListAsync();

            // ── All ENTRY movements (checkin) ────────────────────────────────────────
            var allEntryMovements = await db.AssetMovements
                .Include(m => m.Asset)
                .Where(m => (m.ReaderId != null && entryReaderIds.Contains(m.ReaderId.Value)) || m.MovementType.ToUpper().Contains("CHECKIN") || m.MovementType.ToUpper().Contains("ENTRY"))
                .OrderByDescending(m => m.MovementDate)
                .Take(50)
                .ToListAsync();

            // ── Build result per Driver ──────────────────────────────────────────────
            var drivers = await db.Drivers.ToListAsync();
            var resultTrucks = new List<object>();

            // Also handle trucks if they exist
            var trucks = await db.Trucks.Include(t => t.RfidTag).ToListAsync();

            // Build driver list — from Drivers table + any truck-linked drivers
            var processedDriverIds = new System.Collections.Generic.HashSet<Guid>();

            foreach (var driver in drivers)
            {
                if (processedDriverIds.Contains(driver.Id)) continue;
                processedDriverIds.Add(driver.Id);

                // Get AssetIds associated with this driver via AssetAssignments.CustodianName
                var driverAssetIds = await db.AssetAssignments
                    .Where(a => a.CustodianName != null
                             && a.CustodianName.ToLower().Contains(driver.FullName.ToLower()))
                    .Select(a => a.AssetId)
                    .Distinct()
                    .ToListAsync();

                // ── CHECKOUT TABLE: EXIT reader movements for this driver's assets ──
                var checkoutTable = new List<object>();
                DateTime? lastCheckoutTime = null;
                var addedCheckoutIds = new System.Collections.Generic.HashSet<string>();

                IEnumerable<AssetMovement> exitMovements = new List<AssetMovement>();

                if (driverAssetIds.Any())
                {
                    // Filter by driver's assets
                    exitMovements = allExitMovements
                        .Where(m => driverAssetIds.Contains(m.AssetId));
                }

                foreach (var m in exitMovements)
                {
                    var key = m.AssetId.ToString();
                    if (addedCheckoutIds.Contains(key)) continue;
                    addedCheckoutIds.Add(key);

                    var rfidTag = await db.RFIDTags.FirstOrDefaultAsync(rt => rt.AssetId == m.AssetId);
                    if (lastCheckoutTime == null || m.MovementDate > lastCheckoutTime)
                        lastCheckoutTime = m.MovementDate;

                    checkoutTable.Add(new
                    {
                        equipment = m.Asset?.Name ?? m.Asset?.AssetNumber ?? "Unknown Equipment",
                        tagName = rfidTag?.EpcCode ?? "",
                        equipmentType = "RFID_CHECKOUT",
                        detected = "Yes",
                        checkOutDate = m.MovementDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        equipmentId = m.AssetId.ToString()
                    });
                }

                // Also pull from TruckEquipmentAssignments if truck linked
                var linkedTruck = trucks.FirstOrDefault(t => t.DriverId == driver.Id);
                if (linkedTruck != null)
                {
                    var truckAssignments = await db.TruckEquipmentAssignments
                        .Where(a => a.TruckId == linkedTruck.TruckId && a.ReturnedAt == null)
                        .ToListAsync();

                    foreach (var a in truckAssignments)
                    {
                        var key = a.EquipmentId.ToString();
                        if (addedCheckoutIds.Contains(key)) continue;
                        addedCheckoutIds.Add(key);

                        var asset = await db.Assets.FindAsync(a.EquipmentId);
                        var eq = await db.Equipment.Include(e => e.RfidTag)
                            .FirstOrDefaultAsync(e => e.EquipmentId == a.EquipmentId);

                        if (lastCheckoutTime == null || a.AssignedAt > lastCheckoutTime)
                            lastCheckoutTime = a.AssignedAt;

                        checkoutTable.Add(new
                        {
                            equipment = asset?.Name ?? "Unknown Equipment",
                            tagName = eq?.RfidTag?.TagName ?? "",
                            equipmentType = a.Type ?? "EQUIPMENT",
                            detected = "Yes",
                            checkOutDate = a.AssignedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                            equipmentId = a.EquipmentId.ToString()
                        });
                    }
                }

                // ── CHECKIN TABLE: ENTRY reader movements for this driver's assets ──
                var checkinTable = new List<object>();
                DateTime? lastCheckinTime = null;
                int totalDetected = 0;

                IEnumerable<AssetMovement> entryMovements = new List<AssetMovement>();

                if (driverAssetIds.Any())
                {
                    entryMovements = allEntryMovements
                        .Where(m => driverAssetIds.Contains(m.AssetId));
                }

                foreach (var m in entryMovements)
                {
                    var rfidTag = await db.RFIDTags.FirstOrDefaultAsync(rt => rt.AssetId == m.AssetId);
                    if (lastCheckinTime == null || m.MovementDate > lastCheckinTime)
                        lastCheckinTime = m.MovementDate;

                    checkinTable.Add(new
                    {
                        equipment = m.Asset?.Name ?? m.Asset?.AssetNumber ?? "Unknown Equipment",
                        tagName = rfidTag?.EpcCode ?? "",
                        equipmentType = "RFID_CHECKIN",
                        gateStatus = "Matched",
                        equipmentId = m.AssetId.ToString(),
                        checkInDate = m.MovementDate.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                    totalDetected++;
                }

                // Also pull returned TruckEquipmentAssignments if truck linked
                if (linkedTruck != null)
                {
                    var returnedAssignments = await db.TruckEquipmentAssignments
                        .Where(a => a.TruckId == linkedTruck.TruckId && a.ReturnedAt != null)
                        .OrderByDescending(a => a.ReturnedAt)
                        .Take(10)
                        .ToListAsync();

                    foreach (var a in returnedAssignments)
                    {
                        var asset = await db.Assets.FindAsync(a.EquipmentId);
                        var eq = await db.Equipment.Include(e => e.RfidTag)
                            .FirstOrDefaultAsync(e => e.EquipmentId == a.EquipmentId);

                        if (lastCheckinTime == null || a.ReturnedAt > lastCheckinTime)
                            lastCheckinTime = a.ReturnedAt;

                        checkinTable.Add(new
                        {
                            equipment = asset?.Name ?? "Unknown Equipment",
                            tagName = eq?.RfidTag?.TagName ?? "",
                            equipmentType = a.Type ?? "EQUIPMENT",
                            gateStatus = "Matched",
                            equipmentId = a.EquipmentId.ToString(),
                            checkInDate = a.ReturnedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
                        });
                        totalDetected++;
                    }
                }

                // Missing cases (from MissingEquipmentCases linked to truck)
                int missingCount = 0;
                if (linkedTruck != null)
                {
                    var openCases = await db.MissingEquipmentCases
                        .Include(c => c.Items)
                        .Where(c => c.TruckId == linkedTruck.TruckId && c.ClosedAt == null)
                        .ToListAsync();

                    foreach (var c in openCases)
                    {
                        foreach (var item in c.Items.Where(i => !i.IsRecovered))
                        {
                            var asset = await db.Assets.FindAsync(item.EquipmentId);
                            var eq = await db.Equipment.Include(e => e.RfidTag)
                                .FirstOrDefaultAsync(e => e.EquipmentId == item.EquipmentId);

                            checkinTable.Add(new
                            {
                                equipment = asset?.Name ?? "Unknown Equipment",
                                tagName = eq?.RfidTag?.TagName ?? item.Epc,
                                equipmentType = "EQUIPMENT",
                                gateStatus = "Missing",
                                equipmentId = item.EquipmentId.ToString(),
                                checkInDate = c.OpenedAt.ToString("yyyy-MM-dd HH:mm:ss")
                            });
                            missingCount++;
                        }
                    }
                }

                int totalExpected = totalDetected + missingCount;

                resultTrucks.Add(new
                {
                    truck = new
                    {
                        truckId = linkedTruck?.TruckId.ToString() ?? driver.Id.ToString(),
                        truckNumber = linkedTruck?.TruckNumber ?? $"Individual-{driver.FullName}",
                        driver = driver.FullName
                    },
                    checkOut = new
                    {
                        lastCheckoutTime = lastCheckoutTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                        table = checkoutTable
                    },
                    checkIn = new
                    {
                        lastCheckinTime = lastCheckinTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                        table = checkinTable,
                        summary = new
                        {
                            totalExpected = totalExpected,
                            totalDetected = totalDetected,
                            missingCount = missingCount
                        }
                    }
                });
            }

            // ── Also include standalone AssetAssignment checkouts (Individual / Driver-Based) ──────
            var processedCustodians = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in resultTrucks)
            {
                // Each truck already covers its driver's assignments; mark them processed
                var rObj = r as dynamic;
            }

            var standaloneAssignments = await db.AssetAssignments
                .Include(a => a.Asset)
                .Where(a => a.CustodianName != null && a.CustodianName.Length > 0)
                .OrderByDescending(a => a.AssignedDate)
                .Take(200)
                .ToListAsync();

            // Group by custodian name
            var byCustodian = standaloneAssignments
                .GroupBy(a => a.CustodianName!, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Determine which custodians are already covered by the drivers above
            var coveredCustodians = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in drivers)
                coveredCustodians.Add(d.FullName);

            foreach (var group in byCustodian)
            {
                string custodianName = group.Key;
                if (coveredCustodians.Contains(custodianName)) continue;
                if (custodianName == "Standalone Handheld Operator" || custodianName == "Handheld Operator") continue;

                coveredCustodians.Add(custodianName);

                var checkoutTable = new List<object>();
                var checkinTable = new List<object>();
                DateTime? lastCheckoutTime = null;
                DateTime? lastCheckinTime = null;
                int totalDetected = 0;
                int missingCount = 0;

                foreach (var a in group)
                {
                    var rfidTag = a.Asset != null ? await db.RFIDTags.FirstOrDefaultAsync(rt => rt.AssetId == a.Asset.Id) : null;
                    bool isReturned = a.ActualReturnDate != null || a.Status == "Returned" || a.Status == "Completed";
                    bool isMissing = a.Status == "Missing" && !isReturned;

                    var epc = rfidTag?.EpcCode ?? "";

                    bool isAutoCompleted = a.Status == "Completed" || (a.Notes != null && a.Notes.Contains("Handheld Inventory"));
                    string detectedStatus = isAutoCompleted ? "COMPLETED" : (isReturned ? "RETURNED" : (isMissing ? "MISSING" : ""));

                    checkoutTable.Add(new
                    {
                        equipment = a.Asset?.Name ?? a.Asset?.AssetNumber ?? "Scanned Asset",
                        tagName = epc,
                        equipmentType = a.Notes != null && a.Notes.Contains("Driver") ? "DRIVER_CHECKOUT" : "INDIVIDUAL_CHECKOUT",
                        detected = detectedStatus,
                        checkOutDate = a.AssignedDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        equipmentId = a.AssetId.ToString()
                    });

                    if (lastCheckoutTime == null || a.AssignedDate > lastCheckoutTime)
                        lastCheckoutTime = a.AssignedDate;

                    if (isAutoCompleted)
                    {
                        checkinTable.Add(new
                        {
                            equipment = a.Asset?.Name ?? a.Asset?.AssetNumber ?? "Scanned Asset",
                            tagName = epc,
                            equipmentType = "INDIVIDUAL_CHECKIN",
                            gateStatus = "COMPLETED",
                            equipmentId = a.AssetId.ToString(),
                            checkInDate = a.ActualReturnDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? a.AssignedDate.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                        totalDetected++;
                        if (a.ActualReturnDate > lastCheckinTime)
                            lastCheckinTime = a.ActualReturnDate;
                    }
                    else if (isReturned)
                    {
                        checkinTable.Add(new
                        {
                            equipment = a.Asset?.Name ?? a.Asset?.AssetNumber ?? "Scanned Asset",
                            tagName = epc,
                            equipmentType = "INDIVIDUAL_CHECKIN",
                            gateStatus = "RETURNED",
                            equipmentId = a.AssetId.ToString(),
                            checkInDate = a.ActualReturnDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
                        });
                        totalDetected++;
                        if (a.ActualReturnDate > lastCheckinTime)
                            lastCheckinTime = a.ActualReturnDate;
                    }
                    else
                    {
                        checkinTable.Add(new
                        {
                            equipment = a.Asset?.Name ?? a.Asset?.AssetNumber ?? "Scanned Asset",
                            tagName = epc,
                            equipmentType = "INDIVIDUAL_CHECKIN",
                            gateStatus = "MISSING",
                            equipmentId = a.AssetId.ToString(),
                            checkInDate = "-"
                        });
                        missingCount++;
                    }
                }

                if (checkoutTable.Count == 0) continue;

                resultTrucks.Add(new
                {
                    truck = new
                    {
                        truckId = custodianName,
                        truckNumber = $"Individual-{custodianName}",
                        driver = custodianName
                    },
                    checkOut = new
                    {
                        lastCheckoutTime = lastCheckoutTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                        table = checkoutTable
                    },
                    checkIn = new
                    {
                        lastCheckinTime = lastCheckinTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                        table = checkinTable,
                        summary = new
                        {
                            totalExpected = totalDetected + missingCount,
                            totalDetected = totalDetected,
                            missingCount = missingCount
                        }
                    }
                });
            // ── WAREHOUSE GATE READERS: All fixed reader movements (CheckIn/CheckOut) ──
            var fixedCheckoutTable = new List<object>();
            var fixedCheckinTable = new List<object>();
            DateTime? lastFixedCheckout = null;
            DateTime? lastFixedCheckin = null;

            foreach (var m in allExitMovements.Take(50))
            {
                var rfidTag = await db.RFIDTags.FirstOrDefaultAsync(rt => rt.AssetId == m.AssetId);
                if (lastFixedCheckout == null || m.MovementDate > lastFixedCheckout) lastFixedCheckout = m.MovementDate;
                fixedCheckoutTable.Add(new
                {
                    equipment = m.Asset?.Name ?? m.Asset?.AssetNumber ?? "Fixed Reader Asset",
                    tagName = rfidTag?.EpcCode ?? "",
                    equipmentType = "RFID_CHECKOUT",
                    detected = "Yes",
                    checkOutDate = m.MovementDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    equipmentId = m.AssetId.ToString()
                });
            }

            foreach (var m in allEntryMovements.Take(50))
            {
                var rfidTag = await db.RFIDTags.FirstOrDefaultAsync(rt => rt.AssetId == m.AssetId);
                if (lastFixedCheckin == null || m.MovementDate > lastFixedCheckin) lastFixedCheckin = m.MovementDate;
                fixedCheckinTable.Add(new
                {
                    equipment = m.Asset?.Name ?? m.Asset?.AssetNumber ?? "Fixed Reader Asset",
                    tagName = rfidTag?.EpcCode ?? "",
                    equipmentType = "RFID_CHECKIN",
                    gateStatus = "Matched",
                    equipmentId = m.AssetId.ToString(),
                    checkInDate = m.MovementDate.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            if (fixedCheckoutTable.Count > 0 || fixedCheckinTable.Count > 0)
            {
                resultTrucks.Insert(0, new
                {
                    truck = new
                    {
                        truckId = "Warehouse Gate Readers",
                        truckNumber = "Fixed Readers Gate",
                        driver = "Warehouse Exit/Entry Door"
                    },
                    checkOut = new
                    {
                        lastCheckoutTime = lastFixedCheckout?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                        table = fixedCheckoutTable
                    },
                    checkIn = new
                    {
                        lastCheckinTime = lastFixedCheckin?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                        table = fixedCheckinTable,
                        summary = new
                        {
                            totalExpected = fixedCheckinTable.Count,
                            totalDetected = fixedCheckinTable.Count,
                            missingCount = 0
                        }
                    }
                });
            }

            var firstSiteId = drivers.Any()
                ? (await db.AssetAssignments.FirstOrDefaultAsync())?.AssignedToUserId.ToString() ?? Guid.Empty.ToString()
                : Guid.Empty.ToString();

            return Ok(new
            {
                siteId = firstSiteId,
                totalTrucks = resultTrucks.Count,
                trucks = resultTrucks
            });
        }

    public class LegacyLoginRequest
    {
        public string email { get; set; } = null!;
        public string password { get; set; } = null!;
    }

    public class LegacySaveDriverRequest
    {
        public string fullName { get; set; } = null!;
        public string type { get; set; } = null!;
        public string siteId { get; set; } = null!;
    }

    public class InitializeSessionRequest
    {
        public string mode { get; set; } = null!;
        public string? truckNumber { get; set; }
        public string? personName { get; set; }
        public string? driverId { get; set; }
        public string? rfidTag { get; set; }
        public string siteId { get; set; } = null!;
        public string readerId { get; set; } = null!;
    }

    public class ImportTruckRequest
    {
        public string siteId { get; set; } = null!;
        public List<ImportTruckRow> rows { get; set; } = new();
    }

    public class ImportTruckRow
    {
        public string? truckId { get; set; }
        public string truckNumber { get; set; } = null!;
        public string? driverName { get; set; }
        public string? driverId { get; set; }
        public string siteId { get; set; } = null!;
        public string? rfidTagId { get; set; }
    }
    }
}
