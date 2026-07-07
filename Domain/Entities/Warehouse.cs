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
        public Guid SiteId { get; set; }
        public Site Site { get; set; } = null!;
        public ICollection<Zone> Zones { get; set; } = new List<Zone>();
    }
}
