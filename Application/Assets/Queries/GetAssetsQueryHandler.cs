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
                asset.Location != null ? asset.Location.Name : null,
                asset.DeliveryChallanNo,
                asset.InvoiceNumber,
                asset.InvoiceDate,
                asset.PoNumber,
                asset.Image,
                asset.Site != null ? asset.Site.Name : null,
                asset.Warehouse != null ? asset.Warehouse.Name : null,
                asset.Zone != null ? asset.Zone.Name : null,
                asset.Location != null ? asset.Location.Name : null,
                asset.BalanceQty ?? asset.EntryQty ?? 1,
                asset.EntryQty,
                asset.IssuedQty,
                asset.BalanceQty,
                asset.Unit,
                asset.GpsId ?? (asset.GPSDevices != null && asset.GPSDevices.Any() ? asset.GPSDevices.First().Imei : null),
                asset.RfidTag ?? (asset.RFIDTags != null && asset.RFIDTags.Any() ? asset.RFIDTags.First().EpcCode : null),
                asset.Barcode ?? (asset.Barcodes != null && asset.Barcodes.Any() ? asset.Barcodes.First().BarcodeValue : null),
                asset.CreatedBy,
                asset.CreatedOn,
                asset.UpdatedBy,
                asset.UpdatedOn
            ));
        }
    }
}
