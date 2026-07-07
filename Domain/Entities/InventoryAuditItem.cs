using Domain.Common;
using Domain.Enums;
using System;

namespace Domain.Entities
{
    public class InventoryAuditItem : BaseEntity
    {
        public Guid InventoryAuditId { get; set; }
        public InventoryAudit InventoryAudit { get; set; } = null!;
        public Guid AssetId { get; set; }
        public Asset Asset { get; set; } = null!;
        public Guid? ExpectedLocationId { get; set; }
        public Location? ExpectedLocation { get; set; }
        public Guid? ScannedLocationId { get; set; }
        public Location? ScannedLocation { get; set; }
        public AuditItemStatus Status { get; set; } = AuditItemStatus.Missing;
        public DateTime? ScannedDate { get; set; }
        public string? Notes { get; set; }
    }
}
