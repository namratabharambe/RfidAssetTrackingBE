using Application.DTOs;
using Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Assets.Queries
{
    public class GetAssetByIdQueryHandler
     : IRequestHandler<GetAssetByIdQuery, AssetDto?>
    {
        private readonly IAssetRepository _assetRepository;

        public GetAssetByIdQueryHandler(
            IAssetRepository assetRepository)
        {
            _assetRepository = assetRepository;
        }

        public async Task<AssetDto?> Handle(
            GetAssetByIdQuery request,
            CancellationToken cancellationToken)
        {
            var asset = await _assetRepository.GetByIdAsync(
                request.Id,
                cancellationToken);

            if (asset is null)
            {
                return null;
            }

            return new AssetDto(
                         asset.Id,
                         asset.AssetNumber,
                         asset.Name,
                         asset.Description,
                         asset.SerialNumber,
                         asset.Status.ToString(),
                         asset.AssetCategoryId,
                         asset.QrCode,
                         asset.Group,
                         asset.AssetType,
                         asset.OwnerDepartment,
                         asset.Industry,
                         asset.BusinessUnit,
                         asset.CurrentCustodian,
                         asset.CustodianEmail,
                         asset.Model,
                         asset.WarrantyProvider,
                         asset.PurchaseDate,
                         asset.PurchasePrice,
                         asset.WarrantyExpiryDate,
                         asset.ManufacturerId,
                         asset.SiteId,
                         asset.ZoneId,
                         asset.WarehouseId,
                         asset.LocationId,
                         asset.Location != null ? asset.Location.Name : null
                     );
        }
    }
}
