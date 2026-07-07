using Domain.Common;
using System;

namespace Domain.Entities
{
    public class AssetImage : BaseEntity
    {
        public Guid AssetId { get; set; }
        public Asset Asset { get; set; } = null!;
        public string ImageUrl { get; set; } = null!;
        public bool IsPrimary { get; set; }
    }
}
