using Domain.Common;
using System;

namespace Domain.Entities
{
    public class AssetMovement : BaseEntity
    {
        public Guid AssetId { get; set; }
        public Asset Asset { get; set; } = null!;
        public Guid? SourceLocationId { get; set; }
        public Location? SourceLocation { get; set; }
        public Guid? DestinationLocationId { get; set; }
        public Location? DestinationLocation { get; set; }
        public DateTime MovementDate { get; set; }
        public string MovementType { get; set; } = "RFIDScan";
        public Guid? ReaderId { get; set; }
        public Reader? Reader { get; set; }
        public Guid? HandheldDeviceId { get; set; }
        public HandheldDevice? HandheldDevice { get; set; }
        public string? Remarks { get; set; }
    }
}
