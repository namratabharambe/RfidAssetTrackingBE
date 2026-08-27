using Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Assets.Commands.UpdateAsset
{
    public class UpdateAssetCommandHandler
    : IRequestHandler<UpdateAssetCommand>
    {
        private readonly IAssetRepository _assetRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateAssetCommandHandler(
            IAssetRepository assetRepository,
            IUnitOfWork unitOfWork)
        {
            _assetRepository = assetRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            UpdateAssetCommand request,
            CancellationToken cancellationToken)
        {
            var asset = await _assetRepository.GetByIdAsync(
                request.Id,
                cancellationToken);

            if (asset is null)
                throw new KeyNotFoundException("Asset not found.");

            asset.Update(
                request.AssetNumber,
                request.Name,
                request.AssetCategoryId,
                request.Description,
                request.SerialNumber);

            asset.ChangeStatus(request.Status);

            asset.QrCode = request.QrCode;
            asset.Group = request.Group;
            asset.AssetType = request.AssetType;
            asset.OwnerDepartment = request.OwnerDepartment;
            asset.Industry = request.Industry;
            asset.BusinessUnit = request.BusinessUnit;
            asset.CurrentCustodian = request.CurrentCustodian;
            asset.CustodianEmail = request.CustodianEmail;
            asset.Model = request.Model;
            static DateTime? ToUtc(DateTime? dt) => dt.HasValue ? (dt.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc) : dt.Value.ToUniversalTime()) : null;

            asset.PurchaseDate = ToUtc(request.PurchaseDate);
            asset.PurchasePrice = request.PurchasePrice;
            asset.WarrantyExpiryDate = ToUtc(request.WarrantyExpiryDate);
            asset.ManufacturerId = request.ManufacturerId;
            asset.SiteId = request.SiteId;
            asset.ZoneId = request.ZoneId;
            asset.WarehouseId = request.WarehouseId;
            asset.DeliveryChallanNo = request.DeliveryChallanNo;
            asset.InvoiceNumber = request.InvoiceNumber;
            asset.InvoiceDate = ToUtc(request.InvoiceDate);
            if (request.Image != null) asset.Image = request.Image;
            if (request.EntryQty.HasValue) asset.EntryQty = request.EntryQty;
            if (request.IssuedQty.HasValue) asset.IssuedQty = request.IssuedQty;
            var balance = request.BalanceQty ?? request.BalancedQty;
            if (balance.HasValue) asset.BalanceQty = balance;
            else if (request.EntryQty.HasValue) asset.BalanceQty = request.EntryQty.Value - (asset.IssuedQty ?? 0);
            var unit = request.Unit ?? request.UnitQty;
            if (unit != null) asset.Unit = unit;
            if (request.GpsId != null) asset.GpsId = string.IsNullOrWhiteSpace(request.GpsId) ? null : request.GpsId.Trim();
            if (request.RfidTag != null) asset.RfidTag = string.IsNullOrWhiteSpace(request.RfidTag) ? null : request.RfidTag.Trim();
            if (request.Barcode != null) asset.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();

            asset.UpdatedOn = DateTime.UtcNow;

            _assetRepository.Update(asset);
            await SyncLinkedDevicesAsync(asset, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task SyncLinkedDevicesAsync(Domain.Entities.Asset asset, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(asset.GpsId))
            {
                var imei = asset.GpsId.Trim();
                var gpsRepo = _unitOfWork.Repository<Domain.Entities.GPSDevice>();
                var gpsList = await gpsRepo.GetFilteredAsync(g => g.Imei == imei, cancellationToken);
                var gps = gpsList.FirstOrDefault();
                if (gps != null)
                {
                    gps.AssetId = asset.Id;
                    gps.IsDeleted = false;
                    gpsRepo.Update(gps);
                }
                else
                {
                    await gpsRepo.AddAsync(new Domain.Entities.GPSDevice
                    {
                        Id = Guid.NewGuid(),
                        Imei = imei,
                        AssetId = asset.Id,
                        Status = Domain.Enums.DeviceStatus.Online,
                        BatteryLevel = 100,
                        CreatedOn = DateTime.UtcNow
                    }, cancellationToken);
                }

                // Sync Vehicles for Map
                var vehRepo = _unitOfWork.Repository<Domain.Entities.Vehicle>();
                var vehList = await vehRepo.GetFilteredAsync(v => v.DeviceNum == imei, cancellationToken);
                var veh = vehList.FirstOrDefault();
                if (veh != null)
                {
                    veh.RegName = !string.IsNullOrWhiteSpace(asset.Name) ? asset.Name : veh.RegName;
                    veh.Status = "Online";
                    veh.UpdateTime = DateTime.UtcNow;
                    vehRepo.Update(veh);
                }
                else
                {
                    await vehRepo.AddAsync(new Domain.Entities.Vehicle
                    {
                        Id = Guid.NewGuid(),
                        DeviceNum = imei,
                        RegName = !string.IsNullOrWhiteSpace(asset.Name) ? asset.Name : $"Equipment {imei}",
                        Status = "Online",
                        Lat = 18.6210,
                        Lon = 73.8570,
                        UpdateTime = DateTime.UtcNow
                    }, cancellationToken);
                }
            }

            if (!string.IsNullOrWhiteSpace(asset.RfidTag))
            {
                var epc = asset.RfidTag.Trim();
                var rfidRepo = _unitOfWork.Repository<Domain.Entities.RFIDTag>();
                var rfidList = await rfidRepo.GetFilteredAsync(t => t.EpcCode == epc, cancellationToken);
                var rfid = rfidList.FirstOrDefault();
                if (rfid != null)
                {
                    rfid.AssetId = asset.Id;
                    rfid.IsDeleted = false;
                    rfidRepo.Update(rfid);
                }
                else
                {
                    await rfidRepo.AddAsync(new Domain.Entities.RFIDTag
                    {
                        Id = Guid.NewGuid(),
                        EpcCode = epc,
                        AssetId = asset.Id,
                        Status = Domain.Enums.TagStatus.Active,
                        CreatedOn = DateTime.UtcNow
                    }, cancellationToken);
                }
            }

            if (!string.IsNullOrWhiteSpace(asset.Barcode))
            {
                var bcVal = asset.Barcode.Trim();
                var bcRepo = _unitOfWork.Repository<Domain.Entities.Barcode>();
                var bcList = await bcRepo.GetFilteredAsync(b => b.BarcodeValue == bcVal, cancellationToken);
                var bc = bcList.FirstOrDefault();
                if (bc != null)
                {
                    bc.AssetId = asset.Id;
                    bc.IsDeleted = false;
                    bcRepo.Update(bc);
                }
                else
                {
                    await bcRepo.AddAsync(new Domain.Entities.Barcode
                    {
                        Id = Guid.NewGuid(),
                        BarcodeValue = bcVal,
                        AssetId = asset.Id,
                        Format = "Code128",
                        IsActive = true,
                        CreatedOn = DateTime.UtcNow
                    }, cancellationToken);
                }
            }
        }
    }
}
