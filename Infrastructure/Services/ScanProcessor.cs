using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
                            scanEvent.Status = ScanStatus.Unknown;
                            scanEventRepo.Update(scanEvent);
                            continue;
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
                                SourceLocationId = originalSiteId,
                                DestinationLocationId = destinationLocationId,
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
