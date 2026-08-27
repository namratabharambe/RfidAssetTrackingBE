using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Assets.Commands.CreateAsset
{
    public class CreateAssetCommandHandler
        : IRequestHandler<CreateAssetCommand, Guid>
    {
        private readonly IAssetRepository _assetRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateAssetCommandHandler(
            IAssetRepository assetRepository,
            IUnitOfWork unitOfWork)
        {
            _assetRepository = assetRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(
            CreateAssetCommand request,
            CancellationToken cancellationToken)
        {
            var reqName = request.Name?.Trim();
            var reqNumber = request.AssetNumber?.Trim();

            var allAssets = await _assetRepository.GetAllAsync(cancellationToken);
            var existingAsset = allAssets.FirstOrDefault(a =>
                !a.IsDeleted &&
                ((!string.IsNullOrWhiteSpace(reqName) && !string.IsNullOrWhiteSpace(a.Name) && a.Name.Trim().Equals(reqName, StringComparison.OrdinalIgnoreCase)) ||
                 (!string.IsNullOrWhiteSpace(reqNumber) && !string.IsNullOrWhiteSpace(a.AssetNumber) && a.AssetNumber.Trim().Equals(reqNumber, StringComparison.OrdinalIgnoreCase)))
            );

            if (existingAsset != null)
            {
                if (!string.IsNullOrWhiteSpace(request.AssetNumber)) existingAsset.AssetNumber = request.AssetNumber;
                if (!string.IsNullOrWhiteSpace(request.Name)) existingAsset.Name = request.Name;
                if (request.AssetCategoryId != Guid.Empty) existingAsset.AssetCategoryId = request.AssetCategoryId;

                existingAsset.UpdateDetails(
                    request.Description ?? existingAsset.Description,
                    request.SerialNumber ?? existingAsset.SerialNumber);

                existingAsset.ChangeStatus(request.Status);

                if (request.QrCode != null) existingAsset.QrCode = request.QrCode;
                if (request.Group != null) existingAsset.Group = request.Group;
                if (request.AssetType != null) existingAsset.AssetType = request.AssetType;
                if (request.OwnerDepartment != null) existingAsset.OwnerDepartment = request.OwnerDepartment;
                if (request.Industry != null) existingAsset.Industry = request.Industry;
                if (request.BusinessUnit != null) existingAsset.BusinessUnit = request.BusinessUnit;
                if (request.CurrentCustodian != null) existingAsset.CurrentCustodian = request.CurrentCustodian;
                if (request.CustodianEmail != null) existingAsset.CustodianEmail = request.CustodianEmail;
                if (request.Model != null) existingAsset.Model = request.Model;
                if (request.WarrantyProvider != null) existingAsset.WarrantyProvider = request.WarrantyProvider;
                if (request.PurchaseDate.HasValue) existingAsset.PurchaseDate = ToUtc(request.PurchaseDate);
                if (request.PurchasePrice.HasValue) existingAsset.PurchasePrice = request.PurchasePrice;
                if (request.WarrantyExpiryDate.HasValue) existingAsset.WarrantyExpiryDate = ToUtc(request.WarrantyExpiryDate);
                if (request.ManufacturerId.HasValue) existingAsset.ManufacturerId = request.ManufacturerId;
                if (request.SiteId.HasValue) existingAsset.SiteId = request.SiteId;
                if (request.ZoneId.HasValue) existingAsset.ZoneId = request.ZoneId;
                if (request.WarehouseId.HasValue) existingAsset.WarehouseId = request.WarehouseId;
                if (request.DeliveryChallanNo != null) existingAsset.DeliveryChallanNo = request.DeliveryChallanNo;
                if (request.InvoiceNumber != null) existingAsset.InvoiceNumber = request.InvoiceNumber;
                if (request.InvoiceDate.HasValue) existingAsset.InvoiceDate = ToUtc(request.InvoiceDate);
                if (request.PoNumber != null) existingAsset.PoNumber = request.PoNumber;
                if (request.Image != null) existingAsset.Image = request.Image;
                if (request.EntryQty.HasValue) existingAsset.EntryQty = request.EntryQty;
                if (request.IssuedQty.HasValue) existingAsset.IssuedQty = request.IssuedQty;
                var existingBalance = request.BalanceQty ?? request.BalancedQty;
                if (existingBalance.HasValue) existingAsset.BalanceQty = existingBalance;
                else if (request.EntryQty.HasValue && !existingAsset.BalanceQty.HasValue) existingAsset.BalanceQty = request.EntryQty.Value - (existingAsset.IssuedQty ?? 0);
                var existingUnit = request.Unit ?? request.UnitQty;
                if (existingUnit != null) existingAsset.Unit = existingUnit;
                if (request.GpsId != null) existingAsset.GpsId = string.IsNullOrWhiteSpace(request.GpsId) ? null : request.GpsId.Trim();
                if (request.RfidTag != null) existingAsset.RfidTag = string.IsNullOrWhiteSpace(request.RfidTag) ? null : request.RfidTag.Trim();
                if (request.Barcode != null) existingAsset.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();

                existingAsset.UpdatedOn = DateTime.UtcNow;

                _assetRepository.Update(existingAsset);
                await SyncLinkedDevicesAsync(existingAsset, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return existingAsset.Id;
            }

            var asset = new Asset(
                request.AssetNumber,
                request.Name,
                request.AssetCategoryId);

            asset.UpdateDetails(
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
            asset.PoNumber = request.PoNumber;
            asset.Image = request.Image;
            asset.EntryQty = request.EntryQty;
            asset.IssuedQty = request.IssuedQty ?? 0;
            var balance = request.BalanceQty ?? request.BalancedQty;
            asset.BalanceQty = balance ?? (request.EntryQty.HasValue ? request.EntryQty.Value - (request.IssuedQty ?? 0) : null);
            var unit = request.Unit ?? request.UnitQty;
            asset.Unit = unit;
            asset.GpsId = string.IsNullOrWhiteSpace(request.GpsId) ? null : request.GpsId.Trim();
            asset.RfidTag = string.IsNullOrWhiteSpace(request.RfidTag) ? null : request.RfidTag.Trim();
            asset.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();

            asset.CreatedOn = DateTime.UtcNow;

            await _assetRepository.AddAsync(asset, cancellationToken);
            await SyncLinkedDevicesAsync(asset, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return asset.Id;
        }

        private async Task SyncLinkedDevicesAsync(Asset asset, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(asset.GpsId))
            {
                var imei = asset.GpsId.Trim();
                var gpsRepo = _unitOfWork.Repository<GPSDevice>();
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
                    await gpsRepo.AddAsync(new GPSDevice
                    {
                        Id = Guid.NewGuid(),
                        Imei = imei,
                        AssetId = asset.Id,
                        Status = DeviceStatus.Online,
                        BatteryLevel = 100,
                        CreatedOn = DateTime.UtcNow
                    }, cancellationToken);
                }

                // Sync Vehicles for Map
                var vehRepo = _unitOfWork.Repository<Vehicle>();
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
                    await vehRepo.AddAsync(new Vehicle
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
                var rfidRepo = _unitOfWork.Repository<RFIDTag>();
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
                    await rfidRepo.AddAsync(new RFIDTag
                    {
                        Id = Guid.NewGuid(),
                        EpcCode = epc,
                        AssetId = asset.Id,
                        Status = TagStatus.Active,
                        CreatedOn = DateTime.UtcNow
                    }, cancellationToken);
                }
            }

            if (!string.IsNullOrWhiteSpace(asset.Barcode))
            {
                var bcVal = asset.Barcode.Trim();
                var bcRepo = _unitOfWork.Repository<Barcode>();
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
                    await bcRepo.AddAsync(new Barcode
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
