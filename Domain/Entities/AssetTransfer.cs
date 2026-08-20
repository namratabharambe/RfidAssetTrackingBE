using Domain.Common;
using Domain.Enums;
using System;

namespace Domain.Entities
{
    public class AssetTransfer : BaseEntity
    {
        public Guid AssetId { get; set; }
        public Asset Asset { get; set; } = null!;
        public string? ItemName { get; set; }

        public Guid SourceSiteId { get; set; }
        public Site SourceSite { get; set; } = null!;
        public Guid DestinationSiteId { get; set; }
        public Site DestinationSite { get; set; } = null!;

        public Guid? SourceLocationId { get; set; }
        public Location? SourceLocation { get; set; }

        public Guid? DestinationLocationId { get; set; }
        public Location? DestinationLocation { get; set; }

        public decimal Quantity { get; set; } = 1;
        public string? Unit { get; set; }
        public string? Image { get; set; }
        public string? DeliveryChallanNo { get; set; }
        public string? InvoiceNumber { get; set; }

        public DateTime TransferDate { get; set; }
        public TransferStatus Status { get; set; } = TransferStatus.Pending;
        public Guid RequestedByUserId { get; set; }
        public User RequestedByUser { get; set; } = null!;
        public Guid? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }
        public string? Remarks { get; set; }
    }
}
