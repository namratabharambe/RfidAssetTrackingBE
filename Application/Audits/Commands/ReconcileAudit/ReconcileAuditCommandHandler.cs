using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Audits.Commands.ReconcileAudit
{
    public class ReconcileAuditCommandHandler : IRequestHandler<ReconcileAuditCommand, bool>
    {
        private readonly IUnitOfWork _unitOfWork;

        public ReconcileAuditCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(ReconcileAuditCommand request, CancellationToken cancellationToken)
        {
            var auditRepo = _unitOfWork.Repository<InventoryAudit>();
            var audit = await auditRepo.GetByIdAsync(request.AuditId, cancellationToken);
            if (audit == null) return false;

            // Resolve SiteId to LocationId if needed to protect foreign key constraints
            Guid? scannedLocationId = request.ScannedLocationId;
            Guid? resolvedSiteId = null;
            if (scannedLocationId.HasValue)
            {
                var isSite = await _unitOfWork.Repository<Site>().GetByIdAsync(scannedLocationId.Value, cancellationToken) != null;
                if (isSite)
                {
                    resolvedSiteId = scannedLocationId.Value;
                    var locations = await _unitOfWork.Repository<Location>().GetAllAsync(cancellationToken);
                    var firstLoc = locations.FirstOrDefault();
                    if (firstLoc != null)
                    {
                        scannedLocationId = firstLoc.Id;
                    }
                    else
                    {
                        // Create dummy warehouse/zone/location structure to preserve constraint integrity
                        var firstWarehouse = (await _unitOfWork.Repository<Warehouse>().GetAllAsync(cancellationToken)).FirstOrDefault();
                        if (firstWarehouse == null)
                        {
                            firstWarehouse = new Warehouse { Id = Guid.NewGuid(), Code = "WH-AUDIT", Name = "Audit Warehouse", CreatedOn = DateTime.UtcNow };
                            await _unitOfWork.Repository<Warehouse>().AddAsync(firstWarehouse, cancellationToken);
                        }
                        var firstZone = (await _unitOfWork.Repository<Zone>().GetFilteredAsync(z => z.WarehouseId == firstWarehouse.Id, cancellationToken)).FirstOrDefault();
                        if (firstZone == null)
                        {
                            firstZone = new Zone { Id = Guid.NewGuid(), Code = "Z-AUDIT", Name = "Audit Zone", WarehouseId = firstWarehouse.Id, CreatedOn = DateTime.UtcNow };
                            await _unitOfWork.Repository<Zone>().AddAsync(firstZone, cancellationToken);
                        }
                        var defaultLoc = new Location { Id = Guid.NewGuid(), Code = "LOC-AUDIT", Name = "Audit Location", ZoneId = firstZone.Id, CreatedOn = DateTime.UtcNow };
                        await _unitOfWork.Repository<Location>().AddAsync(defaultLoc, cancellationToken);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        scannedLocationId = defaultLoc.Id;
                    }
                }
            }


            // Fetch expected items for this audit
            var auditItemRepo = _unitOfWork.Repository<InventoryAuditItem>();
            var expectedItems = await auditItemRepo.GetFilteredAsync(x => x.InventoryAuditId == request.AuditId, cancellationToken);
            var expectedItemsList = expectedItems.ToList();

            // Match scanned EPCs with RFIDTags to find AssetIds
            var tagRepo = _unitOfWork.Repository<RFIDTag>();
            var assetRepo = _unitOfWork.Repository<Asset>();
            var movementRepo = _unitOfWork.Repository<AssetMovement>();

            var scannedAssetIds = new HashSet<Guid>();
            var epcToAssetIdMap = new Dictionary<string, Guid>();

            foreach (var epc in request.ScannedEpcs.Select(e => e.Trim().ToLower()).Distinct())
            {
                var tags = await tagRepo.GetFilteredAsync(t => t.EpcCode.Trim().ToLower() == epc, cancellationToken);
                var tag = tags.FirstOrDefault();
                if (tag != null && tag.AssetId.HasValue)
                {
                    scannedAssetIds.Add(tag.AssetId.Value);
                    epcToAssetIdMap[epc] = tag.AssetId.Value;
                }
            }

            // 1. Process expected items
            foreach (var item in expectedItemsList)
            {
                if (scannedAssetIds.Contains(item.AssetId))
                {
                    item.Status = AuditItemStatus.Found;
                    item.ScannedLocationId = scannedLocationId;
                    item.ScannedDate = DateTime.UtcNow;
                    item.Notes = "Verified present during audit scan.";
                    scannedAssetIds.Remove(item.AssetId); // Item handled
                }
                else
                {
                    item.Status = AuditItemStatus.Missing;
                    item.Notes = "Asset expected but missing in physical scan.";
                }
                auditItemRepo.Update(item);
            }

            // 2. Process misplaced/unexpected items (remaining in scannedAssetIds)
            foreach (var assetId in scannedAssetIds)
            {
                var asset = await assetRepo.GetByIdAsync(assetId, cancellationToken);
                if (asset != null)
                {
                    var originalSiteId = asset.SiteId;
                    var originalLocationId = asset.LocationId;

                    // Update asset location to new physical location
                    if (scannedLocationId.HasValue)
                    {
                        asset.SiteId = resolvedSiteId;
                        asset.LocationId = scannedLocationId;
                        assetRepo.Update(asset);

                        // Log an AssetMovement record for this physical relocation correction
                        var movement = new AssetMovement
                        {
                            Id = Guid.NewGuid(),
                            AssetId = asset.Id,
                            SourceLocationId = originalLocationId,
                            DestinationLocationId = scannedLocationId,
                            MovementDate = DateTime.UtcNow,
                            MovementType = "AuditCorrection",
                            Remarks = $"Location corrected automatically via audit run '{audit.Title}'."
                        };
                        await movementRepo.AddAsync(movement, cancellationToken);
                    }

                    var misplacedItem = new InventoryAuditItem
                    {
                        Id = Guid.NewGuid(),
                        InventoryAuditId = audit.Id,
                        AssetId = assetId,
                        ExpectedLocationId = originalLocationId,
                        ScannedLocationId = scannedLocationId,
                        Status = AuditItemStatus.Misplaced,
                        ScannedDate = DateTime.UtcNow,
                        Notes = $"Misplaced asset. Expected at location {originalLocationId?.ToString() ?? "N/A"}, found at {scannedLocationId?.ToString() ?? "N/A"}."
                    };
                    await auditItemRepo.AddAsync(misplacedItem, cancellationToken);
                }
            }

            // Update audit status to Completed
            audit.Status = AuditStatus.Completed;
            auditRepo.Update(audit);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
