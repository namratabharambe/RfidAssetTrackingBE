using Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Assets.Commands.UpdateAsset
{

    public sealed record UpdateAssetCommand(
        Guid Id,
        string? AssetNumber,
        string Name,
        Guid AssetCategoryId,
        string? Description,
        string? SerialNumber,
        AssetStatus Status,
        string? QrCode,
        string? Group,
        string? AssetType,
        string? OwnerDepartment,
        string? Industry,
        string? BusinessUnit,
        string? CurrentCustodian,
        string? CustodianEmail,
        string? Model,
        string? WarrantyProvider,
        DateTime? PurchaseDate,
        decimal? PurchasePrice,
        DateTime? WarrantyExpiryDate,
        Guid? ManufacturerId,
        Guid? SiteId,
        Guid? ZoneId,
        Guid? WarehouseId,
        string? DeliveryChallanNo = null,
        string? InvoiceNumber = null,
        DateTime? InvoiceDate = null,
        string? PoNumber = null,
        string? Image = null)
        : IRequest;
}
