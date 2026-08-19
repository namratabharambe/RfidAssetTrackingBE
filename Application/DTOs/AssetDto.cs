using System;

namespace Application.DTOs;

public record AssetDto(
    Guid Id,
    string AssetNumber,
    string Name,
    string? Description,
    string? SerialNumber,
    string Status,
    Guid AssetCategoryId,
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
    Guid? LocationId,
    string? LocationName,
    string? DeliveryChallanNo = null,
    string? InvoiceNumber = null,
    DateTime? InvoiceDate = null,
    string? PoNumber = null,
    string? Image = null
);

public record CreateAssetDto(
    string AssetNumber,
    string Name,
    Guid AssetCategoryId,
    string? Description,
    string? SerialNumber,
    string Status,
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
    Guid? LocationId,
    string? DeliveryChallanNo = null,
    string? InvoiceNumber = null,
    DateTime? InvoiceDate = null,
    string? PoNumber = null,
    string? Image = null
);

public record UpdateAssetDto(
    string AssetNumber,
    string Name,
    Guid AssetCategoryId,
    string? Description,
    string? SerialNumber,
    string Status,
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
    Guid? LocationId,
    string? DeliveryChallanNo = null,
    string? InvoiceNumber = null,
    DateTime? InvoiceDate = null,
    string? PoNumber = null,
    string? Image = null
);

public record AssetCodeOptionDto(
    string Type,
    Guid Id,
    string Code,
    string Name,
    string DisplayLabel
);

public record AssetCodeResponseDto(
    string ContextType,
    string Code,
    string Name,
    List<AssetCodeOptionDto> Options
);
