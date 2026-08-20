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

                existingAsset.UpdatedOn = DateTime.UtcNow;

                _assetRepository.Update(existingAsset);
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

            asset.CreatedOn = DateTime.UtcNow;

            await _assetRepository.AddAsync(asset, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return asset.Id;
        }
    }
}
