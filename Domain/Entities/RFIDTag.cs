using Domain.Common;
using Domain.Enums;
using System;

namespace Domain.Entities
{
    public class RFIDTag : BaseEntity
    {
        public string EpcCode { get; set; } = null!;
        public string? TidCode { get; set; }
        public TagStatus Status { get; set; } = TagStatus.Active;
        public Guid? AssetId { get; set; }
        public Asset? Asset { get; set; }
    }
}
