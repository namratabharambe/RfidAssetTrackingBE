using Application.DTOs;
using Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Assets.Queries
{
    public class GetAssetsQueryHandler
     : IRequestHandler<GetAssetsQuery, IEnumerable<AssetDto>>
    {
        private readonly IAssetRepository _assetRepository;

        public GetAssetsQueryHandler(IAssetRepository assetRepository)
        {
            _assetRepository = assetRepository;
        }

        public async Task<IEnumerable<AssetDto>> Handle(
            GetAssetsQuery request,
            CancellationToken cancellationToken)
        {
            var assets = await _assetRepository.GetAllAsync(cancellationToken);

            return assets.Select(asset => new AssetDto(
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
            ));
        }
    }
}
