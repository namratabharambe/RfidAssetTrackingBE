using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    public class Asset : BaseEntity
    {
        public string AssetNumber { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? SerialNumber { get; set; }
        public AssetStatus Status { get; set; } = AssetStatus.Available;
        
        public Guid AssetCategoryId { get; set; }
        public AssetCategory AssetCategory { get; set; } = null!;
        
        public Guid? ManufacturerId { get; set; }
        public Manufacturer? Manufacturer { get; set; }
        
        public Guid? VendorId { get; set; }
        public Vendor? Vendor { get; set; }
        
        public Guid? SiteId { get; set; }
        public Site? Site { get; set; }
        
        public Guid? WarehouseId { get; set; }
        public Warehouse? Warehouse { get; set; }
        
        public Guid? ZoneId { get; set; }
        public Zone? Zone { get; set; }
        
        public Guid? LocationId { get; set; }
        public Location? Location { get; set; }
        
        public DateTime? PurchaseDate { get; set; }
        public decimal? PurchasePrice { get; set; }
        public DateTime? WarrantyExpiryDate { get; set; }
        
        public string? QrCode { get; set; }
        public string? Group { get; set; }
        public string? AssetType { get; set; }
        public string? OwnerDepartment { get; set; }
        public string? Industry { get; set; }
        public string? BusinessUnit { get; set; }
        public string? CurrentCustodian { get; set; }
        public string? CustodianEmail { get; set; }
        public string? Model { get; set; }
        public string? WarrantyProvider { get; set; }
        public string? DeliveryChallanNo { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string? PoNumber { get; set; }
        public string? Image { get; set; }
        
        public ICollection<AssetImage> AssetImages { get; set; } = new List<AssetImage>();
        public ICollection<RFIDTag> RFIDTags { get; set; } = new List<RFIDTag>();
        public ICollection<Barcode> Barcodes { get; set; } = new List<Barcode>();
        public ICollection<GPSDevice> GPSDevices { get; set; } = new List<GPSDevice>();
        
        public ICollection<AssetAssignment> AssetAssignments { get; set; } = new List<AssetAssignment>();
        public ICollection<AssetTransfer> AssetTransfers { get; set; } = new List<AssetTransfer>();
        public ICollection<AssetMovement> AssetMovements { get; set; } = new List<AssetMovement>();
        public ICollection<AssetTag> AssetTags { get; set; } = new List<AssetTag>();
        public ICollection<AssetTransaction> AssetTransactions { get; set; } = new List<AssetTransaction>();
        public ICollection<Alert> Alerts { get; set; } = new List<Alert>();

        public Asset()
        {
        }

        public Asset(
            string assetNumber,
            string name,
            Guid assetCategoryId)
        {
            AssetNumber = assetNumber;
            Name = name;
            AssetCategoryId = assetCategoryId;
            Status = AssetStatus.Available;
        }

        public void Update(
           string assetNumber,
           string name,
           Guid assetCategoryId,
           string? description,
           string? serialNumber)
        {
            AssetNumber = assetNumber;
            Name = name;
            AssetCategoryId = assetCategoryId;
            Description = description;
            SerialNumber = serialNumber;
        }

        public void UpdateDetails(
            string? description,
            string? serialNumber)
        {
            Description = description;
            SerialNumber = serialNumber;
        }

        public void ChangeStatus(AssetStatus status)
        {
            Status = status;
        }

        public void AddTag(string tagNumber)
        {
            AssetTags.Add(new AssetTag(tagNumber, Id));
        }
    }
}
