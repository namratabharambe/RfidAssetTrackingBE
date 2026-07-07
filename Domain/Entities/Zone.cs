using Domain.Common;
using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    public class Zone : BaseEntity
    {
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public Guid WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; } = null!;
        public ICollection<Location> Locations { get; set; } = new List<Location>();
    }
}
