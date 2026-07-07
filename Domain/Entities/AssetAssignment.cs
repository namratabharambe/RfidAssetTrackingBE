using Domain.Common;
using System;

namespace Domain.Entities
{
    public class AssetAssignment : BaseEntity
    {
        public Guid AssetId { get; set; }
        public Asset Asset { get; set; } = null!;
        public Guid AssignedToUserId { get; set; }
        public User AssignedToUser { get; set; } = null!;
        public string? CustodianName { get; set; }
        public DateTime AssignedDate { get; set; }
        public DateTime? ExpectedReturnDate { get; set; }
        public DateTime? ActualReturnDate { get; set; }
        public string? Purpose { get; set; }
        public string Status { get; set; } = "Active";
        public string? Notes { get; set; }
    }
}
