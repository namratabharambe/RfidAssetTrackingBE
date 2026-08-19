using Domain.Common;
using System;

namespace Domain.Entities
{
    public class AssetIssuance : BaseEntity
    {
        public string IssueCode { get; set; } = null!;
        public Guid AssetId { get; set; }
        public Asset Asset { get; set; } = null!;
        public string AssetNumber { get; set; } = null!;
        public string AssetName { get; set; } = null!;
        public string IssuedToPerson { get; set; } = null!;
        public string Contractor { get; set; } = null!;
        public decimal IssueQuantity { get; set; }
        public string Unit { get; set; } = "Sacks";
        public string Purpose { get; set; } = null!;
        public Guid SiteId { get; set; }
        public Site Site { get; set; } = null!;
        public string SiteName { get; set; } = null!;
        public DateTime IssuedDate { get; set; } = DateTime.UtcNow;
        public decimal PreviousIssuedQty { get; set; }
        public decimal NewIssuedQty { get; set; }
        public decimal PreviousBalanceQty { get; set; }
        public decimal NewBalanceQty { get; set; }
        public Guid? IssuedByUserId { get; set; }
        public User? IssuedByUser { get; set; }
        public string? Remarks { get; set; }
    }
}
