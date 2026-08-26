using Domain.Common;
using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    public class Warehouse : BaseEntity
    {
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Address { get; set; }
        public ICollection<Zone> Zones { get; set; } = new List<Zone>();
    }
}
