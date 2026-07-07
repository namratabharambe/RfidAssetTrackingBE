using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class AssetCategory : BaseEntity
    {
        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public ICollection<Asset> Assets { get; set; } = [];
    }
}
