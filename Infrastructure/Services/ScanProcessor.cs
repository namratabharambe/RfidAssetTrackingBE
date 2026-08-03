using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class ScanProcessor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<AssetTrackingHub> _hubContext;
        private readonly ILogger<ScanProcessor> _logger;

        public ScanProcessor(
            IServiceProvider serviceProvider,
            IHubContext<AssetTrackingHub> hubContext,
            ILogger<ScanProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Scan Processor Background Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AssetTrackingDbContext>();
                    
                    var scanEventRepo = unitOfWork.Repository<ScanEvent>();
                    var rfidTagRepo = unitOfWork.Repository<RFIDTag>();
                    var assetRepo = unitOfWork.Repository<Asset>();
                    var movementRepo = unitOfWork.Repository<AssetMovement>();

                    var scanEvents = await scanEventRepo.GetFilteredAsync(x => x.Status == ScanStatus.Matched, stoppingToken);

                    foreach (var scanEvent in scanEvents)
                    {
                        var tenSecondsAgo = scanEvent.Timestamp.AddSeconds(-10);
                        var duplicates = await scanEventRepo.GetFilteredAsync(x => 
                            x.EpcCode == scanEvent.EpcCode && 
                            x.ReaderId == scanEvent.ReaderId && 
                            x.Id != scanEvent.Id && 
                            x.Timestamp >= tenSecondsAgo, stoppingToken);

                        if (duplicates.Any())
                        {
                            scanEvent.Status = ScanStatus.Duplicate;
                            scanEventRepo.Update(scanEvent);
                            continue;
                        }

                        var epc = scanEvent.EpcCode.Replace(" ", "").ToLower();
                        var tags = await rfidTagRepo.GetFilteredAsync(x => x.EpcCode.Replace(" ", "").ToLower() == epc, stoppingToken);
                        var tag = tags.FirstOrDefault();

                        if (tag == null || tag.AssetId == null)
                        {
                            var cleanEpc = epc.ToUpper();
                            var assetSuffix = cleanEpc.Length >= 6 ? cleanEpc.Substring(cleanEpc.Length - 6) : cleanEpc;
                            var cat = await dbContext.AssetCategories.FirstOrDefaultAsync(stoppingToken);
                            var defaultCatId = cat?.Id ?? Guid.Parse("019f3c62-cbca-75ec-a0a1-568c034f1200");

                            var newAsset = new Asset
                            {
                                Id = Guid.NewGuid(),
                                AssetNumber = $"AST-{assetSuffix}",
                                Name = $"Scanned Asset ({assetSuffix})",
                                AssetCategoryId = defaultCatId,
                                Status = AssetStatus.Available,
                                CreatedOn = DateTime.UtcNow
                            };
                            await assetRepo.AddAsync(newAsset, stoppingToken);

                            if (tag == null)
                            {
                                tag = new RFIDTag
                                {
                                    Id = Guid.NewGuid(),
                                    EpcCode = scanEvent.EpcCode,
                                    AssetId = newAsset.Id,
                                    Status = TagStatus.Active,
                                    CreatedOn = DateTime.UtcNow
                                };
                                await rfidTagRepo.AddAsync(tag, stoppingToken);
                            }
                            else
                            {
                                tag.AssetId = newAsset.Id;
                                rfidTagRepo.Update(tag);
                            }
                            await unitOfWork.SaveChangesAsync(stoppingToken);
                        }

                        var asset = await assetRepo.GetByIdAsync(tag.AssetId.Value, stoppingToken);
                        if (asset != null)
                        {
                            Guid? originalSiteId = asset.SiteId;
                            Guid? originalLocationId = asset.LocationId;
                            Guid? scannedLocationId = asset.LocationId;
                            Guid? destinationLocationId = null;
                             if (scanEvent.ReaderId != null && scanEvent.HandheldDeviceId == null)
                             {
                                 var reader = await unitOfWork.Repository<Reader>().GetByIdAsync(scanEvent.ReaderId.Value, stoppingToken);
                                 if (reader != null)
                                 {
                                     asset.SiteId = reader.SiteId;
                                     
                                     // Resolve Location under reader's SiteId
                                     var locations = await unitOfWork.Repository<Location>().GetFilteredAsync(l => l.Zone.Warehouse.SiteId == reader.SiteId, stoppingToken, l => l.Zone.Warehouse);
                                     var firstLoc = locations.FirstOrDefault();
                                     if (firstLoc != null)
                                     {
                                         scannedLocationId = firstLoc.Id;
                                     }

                                     // Handheld Check-In / Check-Out Assignment Logic:
                                     if (reader.Direction != null && (reader.Direction.Trim().ToUpperInvariant() == "ENTRY" || reader.Direction.Trim().ToUpperInvariant() == "EXIT"))
                                     {
                                         _logger.LogInformation($"ScanProcessor: Reader {reader.Id} has direction {reader.Direction}. Checking for ActiveTruckSessions...");
                                         var activeSession = await dbContext.ActiveTruckSessions
                                             .FirstOrDefaultAsync(s => s.ReaderId == reader.Id && s.SiteId == reader.SiteId, stoppingToken);
                                         
                                         if (activeSession != null)
                                         {
                                             _logger.LogInformation($"ScanProcessor: Found active session for Driver {activeSession.DriverId}");
                                             string custodianName = "Handheld Operator";
                                             Driver? driver = null;
                                             AssetTracking.Rfid.Domain.Entities.Truck? truck = null;
                                             if (activeSession.DriverId.HasValue)
                                             {
                                                 driver = await unitOfWork.Repository<Driver>().GetByIdAsync(activeSession.DriverId.Value, stoppingToken);
                                             }
                                             if (activeSession.TruckId.HasValue)
                                             {
                                                 truck = await dbContext.Trucks.FirstOrDefaultAsync(t => t.TruckId == activeSession.TruckId.Value, stoppingToken);
                                             }

                                             if (driver != null && truck != null)
                                                 custodianName = $"{driver.FullName} (Truck: {truck.TruckNumber})";
                                             else if (driver != null)
                                                 custodianName = driver.FullName;
                                             else if (truck != null)
                                                 custodianName = $"Truck: {truck.TruckNumber}";

                                             var isCheckout = reader.Direction.Trim().ToUpperInvariant() == "EXIT";
                                             _logger.LogInformation($"ScanProcessor: IsCheckout = {isCheckout}, CustodianName = {custodianName}");

                                             if (isCheckout)
                                             {
                                                 // CHECK-OUT
                                                 var existingAssignments = await unitOfWork.Repository<AssetAssignment>()
                                                     .GetFilteredAsync(a => a.AssetId == asset.Id && a.ActualReturnDate == null, stoppingToken);
                                                 
                                                 _logger.LogInformation($"ScanProcessor: Existing active assignments for Asset {asset.Id}: {existingAssignments.Count()}");
                                                 
                                                 if (!existingAssignments.Any())
                                                 {
                                                      var defaultUser = await dbContext.Users.FirstOrDefaultAsync(stoppingToken);
                                                      var newAssignment = new AssetAssignment
                                                      {
                                                          Id = Guid.NewGuid(),
                                                          AssetId = asset.Id,
                                                          AssignedToUserId = defaultUser?.Id ?? Guid.Parse("e1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c6d"), // default user / Admin
                                                          CustodianName = custodianName,
                                                          AssignedDate = scanEvent.Timestamp,
                                                          ExpectedReturnDate = scanEvent.Timestamp.AddDays(1),
                                                          Status = "Active",
                                                          Notes = $"Checked out via Handheld Scanner. Reader: {reader.Name}."
                                                      };
                                                      await unitOfWork.Repository<AssetAssignment>().AddAsync(newAssignment, stoppingToken);
                                                      _logger.LogInformation($"ScanProcessor: Added new AssetAssignment {newAssignment.Id} for Asset {asset.Id}");

                                                     asset.Status = AssetStatus.Assigned;
                                                     asset.SiteId = reader.SiteId;
                                                     assetRepo.Update(asset);
                                                 }
                                             }
                                             else
                                             {
                                                 // CHECK-IN
                                                 var existingAssignments = await unitOfWork.Repository<AssetAssignment>()
                                                     .GetFilteredAsync(a => a.AssetId == asset.Id && a.ActualReturnDate == null, stoppingToken);
                                                 foreach (var a in existingAssignments)
                                                 {
                                                     a.ActualReturnDate = scanEvent.Timestamp;
                                                     a.Status = "Returned";
                                                     a.Notes = "Checked in via Handheld Scanner. Status: Found.";
                                                     unitOfWork.Repository<AssetAssignment>().Update(a);
                                                 }

                                                 asset.Status = AssetStatus.Available;
                                                 asset.SiteId = reader.SiteId;
                                                 assetRepo.Update(asset);

                                                 // Verify other tags checked out by this custodian and mark missing if not scanned
                                                 var allOpenAssignments = await unitOfWork.Repository<AssetAssignment>()
                                                     .GetFilteredAsync(a => a.ActualReturnDate == null, stoppingToken, a => a.Asset);

                                                 var custodianOpen = allOpenAssignments.Where(a => 
                                                     a.CustodianName != null && 
                                                     (a.CustodianName.Contains(custodianName) || 
                                                      (driver != null && a.CustodianName.Contains(driver.FullName)) || 
                                                      (truck != null && a.CustodianName.Contains(truck.TruckNumber)))).ToList();

                                                 foreach (var a in custodianOpen)
                                                 {
                                                     var assetTag = await rfidTagRepo.GetFilteredAsync(t => t.AssetId == a.AssetId, stoppingToken);
                                                     var epcCode = assetTag.FirstOrDefault()?.EpcCode;
                                                     if (epcCode != null)
                                                     {
                                                         var scannedInSession = await scanEventRepo.GetFilteredAsync(x => 
                                                             x.ScanSessionId == scanEvent.ScanSessionId && 
                                                             x.EpcCode.Replace(" ", "").ToLower() == epcCode.Replace(" ", "").ToLower(), stoppingToken);

                                                         if (!scannedInSession.Any())
                                                         {
                                                             a.Status = "Missing";
                                                             a.Notes = "Not detected during check-in. Status: Missing.";
                                                             unitOfWork.Repository<AssetAssignment>().Update(a);

                                                             var ass = await assetRepo.GetByIdAsync(a.AssetId, stoppingToken);
                                                             if (ass != null)
                                                             {
                                                                 ass.Status = AssetStatus.Retired; // Missing
                                                                 assetRepo.Update(ass);
                                                             }
                                                         }
                                                     }
                                                 }
                                             }
                                         }
                                         else
                                         {
                                              _logger.LogInformation($"ScanProcessor: Fixed reader scan without active truck session. Reader: {reader.Name}, Direction: {reader.Direction}, Antenna: {scanEvent.AntennaIndex}");
                                              var isExit = (reader.Direction != null && reader.Direction.Trim().ToUpperInvariant() == "EXIT") || scanEvent.AntennaIndex == 1 || scanEvent.AntennaIndex == 2;
                                              var isEntry = (reader.Direction != null && reader.Direction.Trim().ToUpperInvariant() == "ENTRY") || scanEvent.AntennaIndex == 4 || scanEvent.AntennaIndex == 3;

                                              if (isExit)
                                              {
                                                  var existingAssignments = await unitOfWork.Repository<AssetAssignment>()
                                                      .GetFilteredAsync(a => a.AssetId == asset.Id && a.ActualReturnDate == null, stoppingToken);

                                                  if (!existingAssignments.Any())
                                                  {
                                                      var defaultUser = await dbContext.Users.FirstOrDefaultAsync(stoppingToken);
                                                      var newAssignment = new AssetAssignment
                                                      {
                                                          Id = Guid.NewGuid(),
                                                          AssetId = asset.Id,
                                                          AssignedToUserId = defaultUser?.Id ?? Guid.Parse("e1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c6d"),
                                                          CustodianName = "Warehouse Exit/Entry Door",
                                                          AssignedDate = scanEvent.Timestamp,
                                                          ExpectedReturnDate = scanEvent.Timestamp.AddDays(1),
                                                          Status = "Active",
                                                          Purpose = "Fixed Reader Exit",
                                                          Notes = $"Checked out via Fixed Reader Exit (Antenna 1). Reader: {reader.Name}."
                                                      };
                                                      await unitOfWork.Repository<AssetAssignment>().AddAsync(newAssignment, stoppingToken);
                                                      asset.Status = AssetStatus.Assigned;
                                                      asset.SiteId = reader.SiteId;
                                                      assetRepo.Update(asset);

                                                      var exitMovement = new AssetMovement
                                                      {
                                                          Id = Guid.NewGuid(),
                                                          AssetId = asset.Id,
                                                          MovementType = "Exit",
                                                          MovementDate = scanEvent.Timestamp,
                                                          ReaderId = reader.Id,
                                                          Remarks = $"Checked out via Fixed Reader Exit (Antenna 1). Reader: {reader.Name}."
                                                      };
                                                      await unitOfWork.Repository<AssetMovement>().AddAsync(exitMovement, stoppingToken);
                                                  }
                                              }
                                              else if (isEntry)
                                              {
                                                  var existingAssignments = await unitOfWork.Repository<AssetAssignment>()
                                                      .GetFilteredAsync(a => a.AssetId == asset.Id && a.ActualReturnDate == null, stoppingToken);

                                                  if (existingAssignments.Any())
                                                  {
                                                      foreach (var a in existingAssignments)
                                                      {
                                                          a.ActualReturnDate = scanEvent.Timestamp;
                                                          a.Status = "Returned";
                                                          a.Notes = $"Checked in via Fixed Reader Entry (Antenna 4). Status: Found (RETURNED). Reader: {reader.Name}.";
                                                          unitOfWork.Repository<AssetAssignment>().Update(a);
                                                      }
                                                  }
                                                  else
                                                  {
                                                      var defaultUser = await dbContext.Users.FirstOrDefaultAsync(stoppingToken);
                                                      var newAssignment = new AssetAssignment
                                                      {
                                                          Id = Guid.NewGuid(),
                                                          AssetId = asset.Id,
                                                          AssignedToUserId = defaultUser?.Id ?? Guid.Parse("e1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c6d"),
                                                          CustodianName = "Warehouse Exit/Entry Door",
                                                          AssignedDate = scanEvent.Timestamp,
                                                          ActualReturnDate = scanEvent.Timestamp,
                                                          Status = "Returned",
                                                          Purpose = "Fixed Reader Entry",
                                                          Notes = $"Checked in via Fixed Reader Entry (Antenna 4). Status: Found (RETURNED). Reader: {reader.Name}."
                                                      };
                                                      await unitOfWork.Repository<AssetAssignment>().AddAsync(newAssignment, stoppingToken);
                                                  }

                                                  asset.Status = AssetStatus.Available;
                                                  asset.SiteId = reader.SiteId;
                                                  assetRepo.Update(asset);

                                                  var entryMovement = new AssetMovement
                                                  {
                                                      Id = Guid.NewGuid(),
                                                      AssetId = asset.Id,
                                                      MovementType = "Checkin",
                                                      MovementDate = scanEvent.Timestamp,
                                                      ReaderId = reader.Id,
                                                      Remarks = $"Checked in via Fixed Reader Entry (Antenna 4). Status: Found (RETURNED)."
                                                  };
                                                  await unitOfWork.Repository<AssetMovement>().AddAsync(entryMovement, stoppingToken);

                                                  var allOpenAssignments = await unitOfWork.Repository<AssetAssignment>()
                                                      .GetFilteredAsync(a => a.ActualReturnDate == null && a.Status == "Active", stoppingToken, a => a.Asset);

                                                  foreach (var a in allOpenAssignments.Where(a => a.AssetId != asset.Id))
                                                  {
                                                      var assetTag = await rfidTagRepo.GetFilteredAsync(t => t.AssetId == a.AssetId, stoppingToken);
                                                      var epcCode = assetTag.FirstOrDefault()?.EpcCode;
                                                      if (epcCode != null)
                                                      {
                                                          var scannedInSession = await scanEventRepo.GetFilteredAsync(x =>
                                                              x.ScanSessionId == scanEvent.ScanSessionId &&
                                                              x.EpcCode.Replace(" ", "").ToLower() == epcCode.Replace(" ", "").ToLower(), stoppingToken);

                                                          if (!scannedInSession.Any())
                                                          {
                                                              a.Status = "Missing";
                                                              a.Notes = "Not detected during Fixed Reader Entry (Antenna 4) scan. Status: Missing.";
                                                              unitOfWork.Repository<AssetAssignment>().Update(a);

                                                              var ass = await assetRepo.GetByIdAsync(a.AssetId, stoppingToken);
                                                              if (ass != null)
                                                              {
                                                                  ass.Status = AssetStatus.Retired;
                                                                  assetRepo.Update(ass);
                                                              }

                                                              var missingMovement = new AssetMovement
                                                              {
                                                                  Id = Guid.NewGuid(),
                                                                  AssetId = a.AssetId,
                                                                  MovementType = "Missing",
                                                                  MovementDate = scanEvent.Timestamp,
                                                                  ReaderId = reader.Id,
                                                                  Remarks = "Not detected during Fixed Reader Entry (Antenna 4) scan. Status: Missing."
                                                              };
                                                              await unitOfWork.Repository<AssetMovement>().AddAsync(missingMovement, stoppingToken);
                                                          }
                                                      }
                                                  }
                                              }
                                         }
                                     }
                                 }
                             }                             else if (scanEvent.HandheldDeviceId != null)
                                                          {
                                                              var handheld = await unitOfWork.Repository<HandheldDevice>().GetByIdAsync(scanEvent.HandheldDeviceId.Value, stoppingToken);
                                                              if (handheld != null)
                                                              {
                                                                  // Default to the asset's current location first (so it preserves API updates)
                                                                  scannedLocationId = asset.LocationId;
                                                                  if (scanEvent.ReaderId != null)
                                                                  {
                                                                      var reader = await unitOfWork.Repository<Reader>().GetByIdAsync(scanEvent.ReaderId.Value, stoppingToken);
                                                                      if (reader != null)
                                                                      {
                                                                          asset.SiteId = reader.SiteId;
                                                                          var locations = await unitOfWork.Repository<Location>().GetFilteredAsync(l => l.Zone.Warehouse.SiteId == reader.SiteId, stoppingToken, l => l.Zone.Warehouse);
                                                                          var firstLoc = locations.FirstOrDefault();
                                                                          if (firstLoc != null)
                                                                          {
                                                                              scannedLocationId = firstLoc.Id;
                                                                          }
                                                                      }
                                                                  }

                                                                  if (scannedLocationId == null)
                                                                  {
                                                                      var fallbackGuid = Guid.Parse("019f39bb-a292-7f9e-a894-3252a13b4825");
                                                                      var locExists = await unitOfWork.Repository<Location>().GetByIdAsync(fallbackGuid, stoppingToken);
                                                                      if (locExists != null)
                                                                      {
                                                                          scannedLocationId = fallbackGuid;
                                                                      }
                                                                  }
                             
                                                                  var gpsDevices = await unitOfWork.Repository<GPSDevice>().GetFilteredAsync(g => g.Imei == handheld.DeviceSerial, stoppingToken);
                                                                  var gpsDevice = gpsDevices.FirstOrDefault();
                                                                  if (gpsDevice != null)
                                                                  {
                                                                      var histories = await unitOfWork.Repository<GPSHistory>().GetFilteredAsync(h => h.GPSDeviceId == gpsDevice.Id, stoppingToken);
                                                                      var latestGps = histories.OrderByDescending(h => h.Timestamp).FirstOrDefault();
                                                                      if (latestGps != null)
                                                                      {
                                                                          var locations = await unitOfWork.Repository<Location>().GetFilteredAsync(l => l.Latitude != null && l.Longitude != null, stoppingToken);
                                                                          Location closestLocation = null;
                                                                          double minDistance = double.MaxValue;
                             
                                                                          foreach (var loc in locations)
                                                                          {
                                                                              double latDiff = (double)loc.Latitude.Value - latestGps.Latitude;
                                                                              double lonDiff = (double)loc.Longitude.Value - latestGps.Longitude;
                                                                              double dist = Math.Sqrt(latDiff * latDiff + lonDiff * lonDiff);
                             
                                                                              if (dist < minDistance)
                                                                              {
                                                                                  minDistance = dist;
                                                                                  closestLocation = loc;
                                                                              }
                                                                          }
                             
                                                                          if (closestLocation != null)
                                                                          {
                                                                              scannedLocationId = closestLocation.Id;
                                                                          }
                                                                      }
                                                                  }
                             
                                                                  if (scannedLocationId != null)
                                                                  {
                                                                      var locWithSite = await unitOfWork.Repository<Location>().GetByIdAsync(scannedLocationId.Value, stoppingToken, l => l.Zone.Warehouse);
                                                                      if (locWithSite?.Zone?.Warehouse != null)
                                                                      {
                                                                          asset.SiteId = locWithSite.Zone.Warehouse.SiteId;
                                                                      }
                                                                  }
                             
                                                                  // --- HANDHELD ASSIGNMENT LOGIC ---
                                                                   _logger.LogInformation($"ScanProcessor: Handheld {handheld.Id} scanned. Checking for ActiveTruckSessions...");
                                                                   
                                                                   // Query active truck session for this handheld or reader or site
                                                                   var activeSession = await dbContext.ActiveTruckSessions
                                                                       .FirstOrDefaultAsync(s => s.ReaderId == handheld.Id || (scanEvent.ReaderId != null && s.ReaderId == scanEvent.ReaderId), stoppingToken);

                                                                   if (activeSession == null)
                                                                   {
                                                                       activeSession = await dbContext.ActiveTruckSessions
                                                                           .OrderByDescending(s => s.LastUpdated)
                                                                           .FirstOrDefaultAsync(s => asset.SiteId == null || s.SiteId == asset.SiteId, stoppingToken);
                                                                   }
                                                                   
                                                                   if (activeSession != null)
                                                                   {
                                                                       _logger.LogInformation($"ScanProcessor: Found active session for Driver {activeSession.DriverId}");
                                                                       string custodianName = "Handheld Operator";
                                                                       Driver? driver = null;
                                                                       AssetTracking.Rfid.Domain.Entities.Truck? truck = null;
                                                                       if (activeSession.DriverId.HasValue)
                                                                       {
                                                                           driver = await unitOfWork.Repository<Driver>().GetByIdAsync(activeSession.DriverId.Value, stoppingToken);
                                                                       }
                                                                       if (activeSession.TruckId.HasValue)
                                                                       {
                                                                           truck = await dbContext.Trucks.FirstOrDefaultAsync(t => t.TruckId == activeSession.TruckId.Value, stoppingToken);
                                                                       }
                               
                                                                       if (driver != null && truck != null)
                                                                           custodianName = $"{driver.FullName} (Truck: {truck.TruckNumber})";
                                                                       else if (driver != null)
                                                                           custodianName = driver.FullName;
                                                                       else if (truck != null)
                                                                           custodianName = $"Truck: {truck.TruckNumber}";
                             
                                                                      // Determine check-in vs check-out direction from RfidScan
                                                                      var rfidScan = await dbContext.RfidScans
                                                                          .Where(s => s.Epc == scanEvent.EpcCode)
                                                                          .OrderByDescending(s => s.Timestamp)
                                                                          .FirstOrDefaultAsync(stoppingToken);
                             
                                                                      var isCheckout = false;
                                                                      if (rfidScan != null && rfidScan.type != null)
                                                                      {
                                                                          if (rfidScan.type.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) >= 0)
                                                                          {
                                                                              isCheckout = true;
                                                                          }
                                                                      }
                             
                                                                      _logger.LogInformation($"ScanProcessor (Handheld): IsCheckout = {isCheckout}, CustodianName = {custodianName}");
                             
                                                                      var siteGuid = activeSession.SiteId;
                                                                      asset.SiteId = siteGuid;
                             
                                                                      if (isCheckout)
                                                                      {
                                                                          // CHECK-OUT
                                                                          var existingAssignments = await unitOfWork.Repository<AssetAssignment>()
                                                                              .GetFilteredAsync(a => a.AssetId == asset.Id && a.ActualReturnDate == null, stoppingToken);
                                                                          
                                                                          _logger.LogInformation($"ScanProcessor (Handheld): Existing active assignments for Asset {asset.Id}: {existingAssignments.Count()}");
                                                                          
                                                                          if (!existingAssignments.Any())
                                                                          {
                                                                              var defaultUser = await dbContext.Users.FirstOrDefaultAsync(stoppingToken);
                                                                              var newAssignment = new AssetAssignment
                                                                              {
                                                                                  Id = Guid.NewGuid(),
                                                                                  AssetId = asset.Id,
                                                                                  AssignedToUserId = defaultUser?.Id ?? Guid.Parse("e1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c6d"), // default user / Admin
                                                                                  CustodianName = custodianName,
                                                                                  AssignedDate = scanEvent.Timestamp,
                                                                                  ExpectedReturnDate = scanEvent.Timestamp.AddDays(1),
                                                                                  Status = "Active",
                                                                                  Notes = $"Checked out via Handheld Scanner. Device: {handheld.Name}."
                                                                              };
                                                                              await unitOfWork.Repository<AssetAssignment>().AddAsync(newAssignment, stoppingToken);
                                                                              _logger.LogInformation($"ScanProcessor (Handheld): Added new AssetAssignment {newAssignment.Id} for Asset {asset.Id}");
                             
                                                                              asset.Status = AssetStatus.Assigned;
                                                                              assetRepo.Update(asset);
                                                                          }
                                                                          else
                                                                          {
                                                                              foreach (var existing in existingAssignments)
                                                                              {
                                                                                  existing.CustodianName = custodianName;
                                                                                  existing.Notes = $"Re-checked out via Handheld Scanner. Device: {handheld.Name}. Operator: {custodianName}";
                                                                                  unitOfWork.Repository<AssetAssignment>().Update(existing);
                                                                              }
                                                                              asset.Status = AssetStatus.Assigned;
                                                                              assetRepo.Update(asset);
                                                                          }
                                                                      }
                                                                      else
                                                                      {
                                                                          // CHECK-IN
                                                                          var existingAssignments = await unitOfWork.Repository<AssetAssignment>()
                                                                              .GetFilteredAsync(a => a.AssetId == asset.Id && a.ActualReturnDate == null, stoppingToken);
                                                                          foreach (var a in existingAssignments)
                                                                          {
                                                                              a.ActualReturnDate = scanEvent.Timestamp;
                                                                              a.Status = "Returned";
                                                                              a.Notes = $"Checked in via Handheld Scanner: {handheld.Name}. Status: Found.";
                                                                              unitOfWork.Repository<AssetAssignment>().Update(a);
                                                                          }
                             
                                                                          asset.Status = AssetStatus.Available;
                                                                          assetRepo.Update(asset);
                             
                                                                          // Verify other tags checked out by this custodian and mark missing if not scanned
                                                                          var allOpenAssignments = await unitOfWork.Repository<AssetAssignment>()
                                                                              .GetFilteredAsync(a => a.ActualReturnDate == null, stoppingToken, a => a.Asset);
                             
                                                                          var custodianOpen = allOpenAssignments.Where(a => 
                                                                              a.CustodianName != null && 
                                                                              (a.CustodianName.Contains(custodianName) || 
                                                                               (driver != null && a.CustodianName.Contains(driver.FullName)) || 
                                                                               (truck != null && a.CustodianName.Contains(truck.TruckNumber)))).ToList();
                             
                                                                          foreach (var a in custodianOpen)
                                                                          {
                                                                              var assetTag = await rfidTagRepo.GetFilteredAsync(t => t.AssetId == a.AssetId, stoppingToken);
                                                                              var epcCode = assetTag.FirstOrDefault()?.EpcCode;
                                                                              if (epcCode != null)
                                                                              {
                                                                                  var scannedInSession = await scanEventRepo.GetFilteredAsync(x => 
                                                                                      x.ScanSessionId == scanEvent.ScanSessionId && 
                                                                                      x.EpcCode.Replace(" ", "").ToLower() == epcCode.Replace(" ", "").ToLower(), stoppingToken);
                             
                                                                                  if (!scannedInSession.Any())
                                                                                  {
                                                                                      a.Status = "Missing";
                                                                                      a.Notes = "Not detected during check-in. Status: Missing.";
                                                                                      unitOfWork.Repository<AssetAssignment>().Update(a);
                             
                                                                                      var ass = await assetRepo.GetByIdAsync(a.AssetId, stoppingToken);
                                                                                      if (ass != null)
                                                                                      {
                                                                                          ass.Status = AssetStatus.Retired; // Missing
                                                                                          assetRepo.Update(ass);
                                                                                      }
                                                                                  }
                                                                              }
                                                                          }
                                                                      }
                                                                  }
                                                                  else
                                                                  {
                                                                      _logger.LogWarning($"ScanProcessor: No ActiveTruckSession found for Handheld {handheld.Id}. Proceeding with standalone handheld update.");
                                                                      
                                                                      // Standalone Equip check-out/check-in without ActiveSession
                                                                      // Let's check the direction first
                                                                      var rfidScan = await dbContext.RfidScans
                                                                          .Where(s => s.Epc == scanEvent.EpcCode)
                                                                          .OrderByDescending(s => s.Timestamp)
                                                                          .FirstOrDefaultAsync(stoppingToken);
                             
                                                                      var isCheckout = false;
                                                                      if (rfidScan != null && rfidScan.type != null)
                                                                      {
                                                                          if (rfidScan.type.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) >= 0)
                                                                          {
                                                                              isCheckout = true;
                                                                          }
                                                                      }
                             
                                                                      _logger.LogInformation($"ScanProcessor (Handheld-Standalone): IsCheckout = {isCheckout}");
                             
                                                                      // Resolve custodian name from ScanSession operator name
                                                                      var custodianNameFallback = "Standalone Handheld Operator";
                                                                      if (scanEvent.ScanSessionId != Guid.Empty)
                                                                      {
                                                                          var session = await dbContext.ScanSessions.FindAsync(new object[] { scanEvent.ScanSessionId }, stoppingToken);
                                                                          if (session != null && session.SessionName != null && session.SessionName.StartsWith("Operator: "))
                                                                          {
                                                                              custodianNameFallback = session.SessionName.Substring("Operator: ".Length).Trim();
                                                                          }
                                                                      }

                                                                      if (isCheckout)
                                                                      {
                                                                          var existingAssignments = await unitOfWork.Repository<AssetAssignment>()
                                                                              .GetFilteredAsync(a => a.AssetId == asset.Id && a.ActualReturnDate == null, stoppingToken);
                                                                          
                                                                          if (!existingAssignments.Any())
                                                                          {
                                                                              var defaultUser = await dbContext.Users.FirstOrDefaultAsync(stoppingToken);
                                                                              var newAssignment = new AssetAssignment
                                                                              {
                                                                                  Id = Guid.NewGuid(),
                                                                                  AssetId = asset.Id,
                                                                                  AssignedToUserId = defaultUser?.Id ?? Guid.Parse("e1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c6d"),
                                                                                  CustodianName = custodianNameFallback,
                                                                                  AssignedDate = scanEvent.Timestamp,
                                                                                  ExpectedReturnDate = scanEvent.Timestamp.AddDays(1),
                                                                                  Status = "Active",
                                                                                  Notes = $"Checked out via Standalone Handheld Scanner. Device: {handheld.Name}. Operator: {custodianNameFallback}"
                                                                              };
                                                                              await unitOfWork.Repository<AssetAssignment>().AddAsync(newAssignment, stoppingToken);
                                                                              
                                                                              asset.Status = AssetStatus.Assigned;
                                                                              assetRepo.Update(asset);
                                                                          }
                                                                          else
                                                                          {
                                                                              foreach (var existing in existingAssignments)
                                                                              {
                                                                                  existing.CustodianName = custodianNameFallback;
                                                                                  existing.Notes = $"Re-checked out via Standalone Handheld Scanner. Device: {handheld.Name}. Operator: {custodianNameFallback}";
                                                                                  unitOfWork.Repository<AssetAssignment>().Update(existing);
                                                                              }
                                                                              asset.Status = AssetStatus.Assigned;
                                                                              assetRepo.Update(asset);
                                                                          }
                                                                      }
                                                                      else
                                                                      {
                                                                          var existingAssignments = await unitOfWork.Repository<AssetAssignment>()
                                                                              .GetFilteredAsync(a => a.AssetId == asset.Id && a.ActualReturnDate == null, stoppingToken);
                                                                          foreach (var a in existingAssignments)
                                                                          {
                                                                              a.ActualReturnDate = scanEvent.Timestamp;
                                                                              a.Status = "Returned";
                                                                              a.Notes = $"Checked in via Standalone Handheld Scanner: {handheld.Name}. Status: Found.";
                                                                              unitOfWork.Repository<AssetAssignment>().Update(a);
                                                                          }
                             
                                                                          asset.Status = AssetStatus.Available;
                                                                          assetRepo.Update(asset);
                                                                      }
                                                                  }
                                                              }
                                                          }

                                                          


                            asset.LocationId = scannedLocationId;
                            assetRepo.Update(asset);

                            // Reconcile with active audits
                            var auditRepo = unitOfWork.Repository<InventoryAudit>();
                            var auditItemRepo = unitOfWork.Repository<InventoryAuditItem>();
                            var activeAudits = await auditRepo.GetFilteredAsync(x => x.Status == AuditStatus.InProgress || x.Status == AuditStatus.Scheduled, stoppingToken);

                            foreach (var audit in activeAudits)
                            {
                                var auditItems = await auditItemRepo.GetFilteredAsync(x => x.InventoryAuditId == audit.Id && x.AssetId == asset.Id, stoppingToken);
                                var item = auditItems.FirstOrDefault();

                                if (item != null)
                                {
                                    if (item.Status != AuditItemStatus.Found)
                                    {
                                        item.Status = AuditItemStatus.Found;
                                        item.ScannedLocationId = scannedLocationId;
                                        item.ScannedDate = scanEvent.Timestamp;
                                        item.Notes = $"Auto-detected by ScanProcessor via reader {scanEvent.ReaderId?.ToString() ?? scanEvent.HandheldDeviceId?.ToString()}.";
                                        auditItemRepo.Update(item);
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
                                        ScannedLocationId = scannedLocationId,
                                        Status = AuditItemStatus.Misplaced,
                                        ScannedDate = scanEvent.Timestamp,
                                        Notes = "Misplaced asset auto-detected by ScanProcessor background worker."
                                    };
                                    await auditItemRepo.AddAsync(misplacedItem, stoppingToken);
                                }

                                if (audit.Status == AuditStatus.Scheduled)
                                {
                                    audit.Status = AuditStatus.InProgress;
                                    auditRepo.Update(audit);
                                }
                            }

                             // Reconcile missing assignments and cases when tag is detected during inventory/audit/reader scans
                             var missingAssignments = await unitOfWork.Repository<AssetAssignment>()
                                 .GetFilteredAsync(a => a.AssetId == asset.Id && a.Status == "Missing", stoppingToken);

                             foreach (var missingAss in missingAssignments)
                             {
                                 missingAss.Status = "Completed";
                                 missingAss.ActualReturnDate = scanEvent.Timestamp;
                                 missingAss.Notes = $"Missing asset recovered via inventory scan. Status: COMPLETED. Timestamp: {scanEvent.Timestamp}.";
                                 unitOfWork.Repository<AssetAssignment>().Update(missingAss);
                             }

                             if (asset.Status == AssetStatus.Retired)
                             {
                                 asset.Status = AssetStatus.Available;
                                 assetRepo.Update(asset);
                             }

                             var openCaseItems = await dbContext.MissingEquipmentCaseItems
                                 .Where(i => i.EquipmentId == asset.Id && !i.IsRecovered)
                                 .ToListAsync(stoppingToken);

                             foreach (var caseItem in openCaseItems)
                             {
                                 caseItem.IsRecovered = true;
                                 caseItem.RecoveredAt = scanEvent.Timestamp;
                             }

                             var movement = new AssetMovement
                            {
                                Id = Guid.NewGuid(),
                                AssetId = asset.Id,
                                SourceLocationId = originalLocationId,
                                DestinationLocationId = scannedLocationId,
                                MovementDate = scanEvent.Timestamp,
                                MovementType = "RFIDScan",
                                ReaderId = scanEvent.ReaderId,
                                HandheldDeviceId = scanEvent.HandheldDeviceId,
                                Remarks = $"Scanned automatically at RFID Reader."
                            };
                            await movementRepo.AddAsync(movement, stoppingToken);

                            await _hubContext.Clients.All.SendAsync("ReceiveLiveScan", new
                            {
                                EpcCode = scanEvent.EpcCode,
                                AssetName = asset.Name,
                                AssetNumber = asset.AssetNumber,
                                Timestamp = scanEvent.Timestamp,
                                Rssi = scanEvent.Rssi,
                                AntennaIndex = scanEvent.AntennaIndex,
                                Location = movement.Remarks
                            }, stoppingToken);
                            scanEvent.Status = ScanStatus.Processed;
                            scanEventRepo.Update(scanEvent);
                        }
                        else
                        {
                            scanEvent.Status = ScanStatus.Unknown;
                            scanEventRepo.Update(scanEvent);
                        }
                    }

                    await unitOfWork.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred in Scan Processor background service.");
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
