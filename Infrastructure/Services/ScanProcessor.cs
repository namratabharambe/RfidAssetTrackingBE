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
                             if (scanEvent.ReaderId != null)
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
                                         var activeSession = await dbContext.ActiveTruckSessions
                                             .FirstOrDefaultAsync(s => s.ReaderId == reader.Id && s.SiteId == reader.SiteId, stoppingToken);
                                         if (activeSession != null)
                                         {
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

                                             if (isCheckout)
                                             {
                                                 // CHECK-OUT
                                                 var existingAssignments = await unitOfWork.Repository<AssetAssignment>()
                                                     .GetFilteredAsync(a => a.AssetId == asset.Id && a.ActualReturnDate == null, stoppingToken);
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
                                     }
                                 }
                             }
                            else if (scanEvent.HandheldDeviceId != null)
                            {
                                var handheld = await unitOfWork.Repository<HandheldDevice>().GetByIdAsync(scanEvent.HandheldDeviceId.Value, stoppingToken);
                                if (handheld != null)
                                {
                                    // Default to the asset's current location first (so it preserves API updates)
                                    scannedLocationId = asset.LocationId;
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
