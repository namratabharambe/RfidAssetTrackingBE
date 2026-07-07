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
            asset.WarrantyProvider = request.WarrantyProvider;
            asset.PurchaseDate = request.PurchaseDate;
            asset.PurchasePrice = request.PurchasePrice;
            asset.WarrantyExpiryDate = request.WarrantyExpiryDate;
            asset.ManufacturerId = request.ManufacturerId;
            asset.SiteId = request.SiteId;
            asset.ZoneId = request.ZoneId;
            asset.WarehouseId = request.WarehouseId;

            asset.CreatedOn = DateTime.UtcNow;

            await _assetRepository.AddAsync(asset, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return asset.Id;
        }
    }
}
