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
            asset.PoNumber = request.PoNumber;
            asset.Image = request.Image;

            asset.UpdatedOn = DateTime.UtcNow;

            _assetRepository.Update(asset);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
